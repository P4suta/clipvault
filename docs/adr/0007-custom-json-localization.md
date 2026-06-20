# 7. Custom JSON localization layer

- Status: accepted
- Date: 2026-06-15
- Deciders: project maintainers
- Tags: architecture, ui

## Context

The UI ships in English, Japanese, and Simplified Chinese. ClipVault is an unpackaged,
self-contained app, so the packaged Windows resource system (`.resw` / PRI / MRT)
cannot reliably override the OS UI language for it — language selection has to be the
app's own, independent of packaging and OS culture.

## Decision

Use a custom localization layer: embedded per-language JSON
(`Localization/Strings/{en,ja,zh-Hans}.json`) loaded at runtime through
`ILocalizationService` and resolved in XAML by the `{loc:Str Key=…}` markup extension
(`StrExtension`). The language is chosen at startup and fixed for the process lifetime
(restart to change).

## Consequences

- Works for an unpackaged, self-contained deployment; language selection is fully under
  the app's control rather than the OS culture.
- No platform tooling — translators edit JSON directly, and every key must exist in
  every language file.
- Restart-to-apply; live language switching is out of scope.

## Alternatives considered

- **`.resw` / PRI / MRT.** Rejected: packaging-coupled; an unpackaged app cannot
  override the OS UI language through MRT.
- **`ResourceManager` / `.resx`.** Rejected: satellite-assembly and culture plumbing
  that is still driven by the OS culture, not the app's own choice.
- **Third-party i18n library.** Rejected: an extra dependency for a small, fixed set of
  strings the custom layer already covers.

## References

- [ARCHITECTURE.md](../ARCHITECTURE.md) — localization pattern.
- `src/ClipVault.App/Localization/StrExtension.cs`,
  `Localization/Strings/`.
