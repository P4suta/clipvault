# Security Policy

ClipVault is a privacy-first clipboard manager. This document covers the threat model, release
verification, and how to report a vulnerability.

## Reporting a vulnerability

Please report security issues **privately** via GitHub's
[Private vulnerability reporting](https://docs.github.com/code-security/security-advisories/guidance-on-reporting-and-writing-information-about-vulnerabilities/privately-reporting-a-security-vulnerability)
on this repository (Security tab → "Report a vulnerability"). Do **not** open a public issue for a
suspected vulnerability.

We aim to acknowledge within a few days and provide an assessment after triage. Include reproduction
steps and the affected version/commit.

## Supported versions

ClipVault is pre-1.0 and ships from `main`. Security fixes target the latest release and `main`.

## Threat model & design

- **Clipboard history at rest** is encrypted per-field with **ChaCha20-Poly1305** (AEAD). Each field uses a
  fresh random 96-bit nonce, and the additional authenticated data binds every ciphertext to its entry id and
  field name (so fields/rows cannot be swapped or replayed). Encryption keys are derived with **HKDF-SHA256**
  (separate keys for AEAD and the keyed duplicate hash).
- **The master key (DEK)** is sealed on disk with Windows **DPAPI** (CurrentUser scope) and, optionally, a
  second factor:
  - **Passphrase** — the key-encryption key is derived with **Argon2id** (64 MiB, 3 iterations, lanes clamped
    to the CPU count) and wraps the DEK with ChaCha20-Poly1305. The Argon2id parameters are stored inside the
    DPAPI envelope, so they are integrity-protected and cannot be downgraded.
  - **Windows Hello** — a TPM-backed credential signs a stored challenge; the KEK is derived from the
    signature. The DEK never leaves the device unprotected.
- **Crypto-erase**: destroying the key file (panic wipe) instantly renders all ciphertext unrecoverable.
- **Volatile mode** keeps the entire history in RAM only; nothing is written to disk and everything is lost on
  exit (the strongest privacy posture).
- **Capture privacy gate**: content is screened before storage. Built-in classifiers reject or mask secrets
  (API keys, JWTs, PEM private keys, credit-card numbers, generic passwords), the OS
  "exclude from clipboard history" signal is honored, and per-application exclusions are supported.
- **Memory hygiene** (defense-in-depth): derived keys and the resolved DEK are zeroed with
  `CryptographicOperations.ZeroMemory` when no longer needed; the passphrase is derived from a pinned,
  immediately-zeroed byte buffer; decrypted clipboard payloads are zeroed after use. Diagnostic output never
  contains full exceptions or plaintext.
- **No persistent system footprint**: OS-level registrations (the global summon hotkey) are runtime-only and
  released on exit.

### Out of scope / accepted limitations

- A `string` in .NET (the entered passphrase) cannot be zeroed and may be relocated by the GC. It originates in
  WinUI's `PasswordBox` and lives in memory only while the vault is unlocked. This is a documented, accepted
  residue; the derived key material is still pinned and zeroed.
- Clipboard contents are inherently user-visible and cross-process by design. Content written back to the OS
  clipboard for a paste is plaintext for as long as it remains on the clipboard; ClipVault marks its own
  clipboard writes to be excluded from Windows Cloud Clipboard/history but does not wipe the OS clipboard.
- An attacker who can read this process's memory or run code as the same Windows user defeats the at-rest
  protections; the design targets offline/file-theft and casual cross-process access, not a compromised
  local account.

## Verifying releases

Every release carries cryptographic **build provenance** (SLSA, via GitHub artifact attestations) and an
attested **SBOM** (CycloneDX), even when the binary is not yet code-signed. See
[docs/VERIFICATION.md](docs/VERIFICATION.md) for step-by-step verification with `gh attestation verify` and the
published `SHA256SUMS.txt`.

## Supply-chain controls

- Central Package Management with committed `packages.lock.json` lockfiles; CI restores in `--locked-mode`.
- `nuget.config` restricts package sources to nuget.org with package source mapping (dependency-confusion
  defense).
- CI runs analyzers (warnings as errors), tests, a dependency vulnerability audit, and CodeQL. Dependabot keeps
  dependencies and SHA-pinned GitHub Actions current.
- Release artifacts get SLSA build-provenance and SBOM attestations bound to the binary's digest.
