# 3. Clean layered architecture

- Status: accepted
- Date: 2026-06-15
- Deciders: project maintainers
- Tags: architecture

## Context

ClipVault is a small, read-only app, but it carries real cryptography, SQLite
persistence, and Windows/WinUI integration. Letting those concerns mix with the
core capture and history logic would make the rules untestable without Windows
and couple privacy-critical behavior to UI code.

## Decision

Adopt a four-layer Clean Architecture: `Domain`, `Application`, `Infrastructure`,
`App`. Project references point inward — `Domain` references nothing, `Application`
references `Domain`, `Infrastructure` references both, and `App` (the composition
root) references all three. `Domain` is framework-free (`net10.0`, BCL only) and
defines the port interfaces (`IClipboardHistoryRepository`, `IEncryptionService`,
`IClock`, …) that `Infrastructure` implements. See [ARCHITECTURE.md](../ARCHITECTURE.md).

## Consequences

- `Domain` and `Application` are `net10.0` and unit-testable off-Windows; capture
  rules and classifiers can be exercised without a clipboard or a UI.
- The dependency rule is explicit and enforceable: a leak shows up as a new project
  reference in review.
- More projects and indirection than a single assembly; ports add a small amount of
  ceremony for each new adapter.

## Alternatives considered

- **Single project.** Rejected: couples the core to WinUI/Windows TFMs, making the
  logic impossible to test off-Windows and blurring the privacy boundary.
- **MVVM-only (logic in view models).** Rejected: business and privacy rules leak
  into presentation, and view models become untestable god-objects.
- **Vertical slices.** Rejected: cryptography and the capture gate cross-cut every
  slice; a layered dependency rule expresses those shared boundaries more clearly.

## References

- [ARCHITECTURE.md](../ARCHITECTURE.md).
- [CONTRIBUTING.md](../../CONTRIBUTING.md) — layered-architecture convention.
