# Clipboard history guide

ClipVault keeps searchable clipboard history on Windows and encrypts stored content with ChaCha20-Poly1305.
Windows encrypts the stored master key for the current user.
Passphrases and Windows Hello provide optional protection.
Volatile mode uses an in-memory history repository.
Capture rules can reject or mask matching secret formats, honor the Windows clipboard privacy signal, and exclude configured apps.
The app runs in the system tray and supports a global hotkey.

The workspace separates the domain, use cases, infrastructure, and WinUI app into four projects.
The [architecture guide](ARCHITECTURE.md) explains their dependencies.

## Setup

Use Windows 10 version 2004 or later, or Windows 11, on x64.
Install [mise](https://mise.jdx.dev/installing-mise.html) before running the source setup.
The repository pins the .NET toolchain, task runner, hooks, and lint tools in `mise.toml` and `mise.lock`.
The recipes use a Unix-compatible shell on Windows.

Run commands from the repository root.
Bootstrap installs the Git hooks, restores local .NET tools, and restores dependencies.

```sh
mise trust
mise install
mise x -- just bootstrap
```

The recipes invoke .NET through mise, so a separate SDK installation isn't required.

## Build and run

Build the x64 app, or stop a resident instance before building and launching it.

```sh
mise x -- just build
mise x -- just run
```

Run the test projects or list the available recipes.

```sh
mise x -- just test
mise x -- just
```

The [development guide](DEVELOPMENT.md) covers the daily loop, editor setup, individual test commands, build channels, and work outside Windows.

## Verification

Run the static checks or the complete local CI gate.

```sh
mise x -- just lint
mise x -- just ci
```

The static gate checks formatting, analyzer warnings, spelling, workflow syntax, YAML, Markdown, and the source suppression policy.
The complete gate adds locked dependency restoration, tests with coverage thresholds, and a vulnerability audit.
Local YAML and Markdown recipes print an installation hint if their optional tools are absent.
CI installs and runs both linters.

Central Package Management defines dependency versions in `Directory.Packages.props`.
Committed `packages.lock.json` files and `nuget.config` constrain dependency restoration.
After changing dependency versions, regenerate the lockfiles and inspect the diff.

```sh
mise x -- just relock
mise x -- just outdated
```

Generate an HTML coverage report with the maintained recipe.

```sh
mise x -- just coverage-html
```

## Troubleshooting

A resident app can hold a build output open.
Run `mise x -- just stop` before rebuilding, or use `mise x -- just run` to stop the app first.
Run `mise x -- just hooks` if Git hooks need installation.
Run `mise x -- just doctor` to check tool resolution, hooks, and the local environment.

## Releases and downloads

An explicit dispatch starts the release workflow.
It verifies the selected source before building a distribution, signing, and publication.
The signing job uses the protected release environment.
Release downloads include a self-contained Windows x64 archive, checksums, a software bill of materials, and provenance attestations.
Signature availability depends on the release's configured signing state.

The archive contains a `ClipVault/` folder.
Start `ClipVault.exe` at its root, with the app and dependencies under `app/`.
Use the [verification guide](VERIFICATION.md) to check checksums, provenance, and signatures for a specific download.
The provenance command takes the downloaded archive path.

```sh
gh attestation verify ClipVault-<version>-win-x64.zip --repo P4suta/clipvault --signer-workflow P4suta/clipvault/.github/workflows/release.yml
```

## Security and contributions

The [security policy](../SECURITY.md) defines the threat model, encryption design, limitations, and private reporting route.
The [contribution guide](../CONTRIBUTING.md) defines the quality gate and commit policy.
Contributors follow the [code of conduct](../CODE_OF_CONDUCT.md).

## License

The existing Apache-2.0 terms are in [LICENSE](../LICENSE).
The repository's [NOTICE](../NOTICE) remains part of the source distribution.
