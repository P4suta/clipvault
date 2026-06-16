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
→ `ClipVault.App` (WinUI 3). Targets .NET 10 on Windows (x64).

## Prerequisites

- Windows 10 2004+ / Windows 11 (x64).
- [mise](https://mise.jdx.dev/) — pins the toolchain (.NET SDK 10.0.300, `just`, `lefthook`) declared in
  `mise.toml`.
- [just](https://github.com/casey/just) — task runner (also provided via mise).

```bash
mise install        # install the pinned toolchain (dotnet, just, lefthook, typos)
just setup          # install git hooks + local dotnet tools, then restore
```

All `dotnet` calls go through `mise exec -- dotnet`, so you do not need a separate .NET install.

## Build & run

```bash
just build          # build the app (x64)
just run            # stop any resident instance, build, and launch
just test           # run all test projects
just check          # pre-commit gate: format check + analyzers (warnings as errors) + tests
just                # list every recipe
```

## Develop

- **Quality gate**: `just check` mirrors CI (format, spell-check via `typos`, analyzers as errors, tests).
  `just ci` adds locked-mode restore and the dependency vulnerability audit.
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
  --repo OWNER/REPO \
  --signer-workflow OWNER/REPO/.github/workflows/release.yml
```

## Security

See **[SECURITY.md](SECURITY.md)** for the threat model, the encryption design, accepted limitations, and how
to privately report a vulnerability.

## License

See the repository for license details.
