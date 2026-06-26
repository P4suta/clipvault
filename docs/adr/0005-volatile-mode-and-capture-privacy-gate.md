# 5. Volatile mode and the capture privacy gate

- Status: accepted
- Date: 2026-06-15
- Deciders: project maintainers
- Tags: architecture, security, privacy

## Context

A privacy-first clipboard manager needs two things the default OS history does not
guarantee: a mode that never touches disk, and a way to stop secrets and sensitive
sources from being captured *before* they reach storage. Filtering after the fact
leaves an exposure window. See [SECURITY.md](../../SECURITY.md).

## Decision

Two facets of one privacy posture.

- **Volatile mode** — `InMemoryClipboardHistoryRepository` paired with
  `EphemeralKeyVault`: the DEK lives only in RAM, is never persisted, and vanishes on
  process exit.
- **Capture privacy gate** — `CaptureGate` runs ordered `ICaptureRule`s rejection-first
  (one rejection aborts capture): `PrivacySignalRule` (honors the OS
  exclude-from-history signals, non-overridable), `SourceAppRule` (per-app exclusion),
  `CaptureStateRule`, `ContentClassificationRule` (classifiers reject or mask API keys,
  PEM private keys, JWTs, credit cards, passwords), `UrlCleaningRule`, `SizeRule`.

## Consequences

- Sensitive content is filtered before persistence; volatile mode leaves no on-disk
  trace at all.
- Classifiers are heuristic — false negatives and positives are possible; the password
  classifier is off by default to avoid over-masking.
- Rule order is significant (rejection-first); adding or reordering rules changes
  behavior and must be reviewed deliberately.

## Update (2026-06-26): bounded memory

Memory is bounded by what is on screen and by fixed per-event/per-store ceilings, never by total
history size. This reaffirms — does not supersede — "volatile leaves no on-disk trace".

- **Volatile mode is a bounded RAM ring.** `InMemoryClipboardHistoryRepository` evicts the oldest
  unpinned entries on every insert to stay within a fixed budget (`VolatileMemoryBudgetBytes`,
  default 256 MB), so RAM cannot grow without bound however much is copied. It holds only recent
  history and stays pure RAM.
- **Large history is a persistent-mode capability.** A history far larger than the volatile budget
  (up to millions of entries) belongs in encrypted-disk mode, where content lives on disk, the list
  is keyset-paged (`GetPageAsync`) with thumbnails fetched on demand, and retention is enforced with
  content-free SQL (`DeleteExpiredAsync`/`TrimAsync`) bounded by a disk quota — so app memory stays
  bounded by the viewport regardless of history size.
- **Single captures are bounded too.** Images are rejected above a megapixel ceiling before the pixel
  plane is allocated (decompression-bomb guard) and text above a byte ceiling before the buffer is
  copied, so no one clipboard event can spike memory.

## Alternatives considered

- **Always persist, then redact.** Rejected: the secret has already hit disk before
  redaction runs.
- **Filter only in the UI.** Rejected: same exposure window; storage still holds the
  raw content.
- **Allow-list capture.** Rejected: too restrictive for a general-purpose clipboard
  tool; rejection-first with classifiers fits the read-most, block-some reality.

## References

- [SECURITY.md](../../SECURITY.md) — privacy gate and volatile mode.
- `src/ClipVault.Application/Capture/CaptureGate.cs` and `Capture/Rules/`,
  `Capture/Classifiers/`.
