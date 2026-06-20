# ClipVault

A privacy-first clipboard history manager for Windows. ClipVault keeps a searchable history of what you copy,
encrypts it at rest, and is built to leave no persistent system footprint.

- **Encrypted at rest** — clipboard content is encrypted per-field with ChaCha20-Poly1305 (AEAD). The master
  key is sealed with Windows DPAPI and can be further protected by a passphrase (Argon2id) or Windows Hello.
- **Volatile mode** — optionally keep the entire history in RAM only; nothing touches the disk.
- **Privacy gate** — secrets (API keys, JWTs, PEM private keys, credit-card numbers, passwords) are rejected or
  masked before they are ever stored, the OS "exclude from clipboard history" signal is honored, and you can
  exclude specific applications.
- **Lightweight & install-free** — an unpackaged, self-contained `.exe`; tray-resident; summon with the global
  hotkey. OS registrations are runtime-only and released on exit.

Architecture: a clean 4-layer split — `ClipVault.Domain` → `ClipVault.Application` → `ClipVault.Infrastructure`
→ `ClipVault.App` (WinUI 3). Targets .NET 10 on Windows (x64). See
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the layer map and where to make changes.

## Prerequisites

- Windows 10 2004+ / Windows 11 (x64).
- [mise](https://mise.jdx.dev/installing-mise.html) — **install this first**; it pins the toolchain
  (.NET SDK 10.0.300, `just`, `lefthook`) declared in `mise.toml`.
- `just` — the task runner, installed by mise (no separate install needed).

```bash
mise install        # install the pinned toolchain (dotnet, just, lefthook, typos, actionlint, committed)
just bootstrap      # install git hooks + local dotnet tools, then restore
```

All `dotnet` calls go through `mise exec -- dotnet`, so you do not need a separate .NET install.

## Build & run

```bash
just build          # build the app (x64)
just run            # stop any resident instance, build, and launch
just test           # run all test projects
just lint           # all static lints: format check + analyzers (warnings as errors) + typos + actionlint/yamllint/markdownlint + strict-code
just                # list every recipe
```

### Troubleshooting

- **Build fails with a locked DLL** — a tray-resident instance holds `ClipVault.App.exe`; run `just stop`
  (or `just run`, which stops first).
- **Hooks not running on commit** — run `just hooks` to (re)install lefthook.
- **`mise: command not found`** — install mise first (see [Prerequisites](#prerequisites)).
- **`yamllint` / `markdownlint` skipped locally** — expected (not pinned by mise); CI enforces them.
- `just doctor` checks the toolchain, hooks, and environment.

## Develop

- **Day-to-day**: see [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) for the dev loop, recipe cheat-sheet,
  IDE setup (`.vscode/` tasks + F5 debug), and the off-Windows story.
- **Quality gate**: `just lint` runs every static check (format, `typos`, analyzers as errors, `actionlint`,
  `yamllint`, `markdownlint`, `strict-code`); `just ci` is the full gate (locked restore + lint + tests +
  dependency vulnerability audit). Commits follow [Conventional Commits](https://www.conventionalcommits.org/),
  enforced by the `committed` commit-msg hook.
- **Dependencies**: pinned via Central Package Management (`Directory.Packages.props`) with committed
  `packages.lock.json` lockfiles. `nuget.config` restricts restore to nuget.org with package source mapping.
  After changing versions run `just relock` to regenerate the lockfiles; `just outdated` lists available updates.
- **Coverage**: `just coverage-html` produces an HTML report.

## Releases & verification

Releases are produced by `.github/workflows/release.yml` on a `v*.*.*` tag. Each release ships a self-contained
build with:

- **SLSA build provenance** and an attested **CycloneDX SBOM**, both bound to the binary's digest.
- `SHA256SUMS.txt` for the assets.
- Code signing (SSL.com) activates automatically once signing secrets are configured; until then binaries are
  unsigned but fully verifiable via provenance.

To verify a download, see **[docs/VERIFICATION.md](docs/VERIFICATION.md)**:

```bash
gh attestation verify ClipVault-<version>-win-x64.zip \
  --repo P4suta/clipvault \
  --signer-workflow P4suta/clipvault/.github/workflows/release.yml
```

## Security

See **[SECURITY.md](SECURITY.md)** for the threat model, the encryption design, accepted limitations, and how
to privately report a vulnerability.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). By participating you agree to the
[Code of Conduct](CODE_OF_CONDUCT.md).

## License

ClipVault is licensed under the Apache License 2.0. See [LICENSE](LICENSE) and [NOTICE](NOTICE).
