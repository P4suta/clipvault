# Development

The daily dev loop. Toolchain is pinned by mise; tasks run through `just` (the Justfile is the single
source of truth — `just` lists every recipe). For contribution policy and the quality gate see
[CONTRIBUTING.md](../CONTRIBUTING.md); for the layer map see [ARCHITECTURE.md](./ARCHITECTURE.md).

## Setup

1. Install mise and bootstrap — see the [README](../README.md#build--run) (`mise install` → `just bootstrap`).
2. Open the project. **Visual Studio / Rider**: open `ClipVault.slnx`. **VS Code**: accept the recommended
   extensions; `.vscode/` provides build/test/run/lint/check tasks (Ctrl+Shift+B builds) and an F5 launch
   config that debugs the unpackaged exe.
3. `just doctor` — verifies the toolchain resolves, git hooks are installed, and the environment is sane.

## Daily loop

- `just run` — stop → build → launch (unpackaged).
- `just check` — fast inner-loop check (incremental App build + style/grep verify); **not** the full gate.
- `just watch-check` — recompile the App on every change (build-only).
- `just fix` — apply every auto-fixer (whitespace, code style, typos); review the diff.
- `just test` / `just test-one Infrastructure` / `just test-filter KeyProtector` — run tests.
- `just watch-test Application` — re-run a test project on change.
- `just lint` — the full static gate (format + analyzers + typos + linters + strict-code); `just pre-commit`
  previews the lefthook chain.

## Recipe cheat-sheet

The daily subset. `just` lists everything; see the [Justfile](../Justfile) for the rest.

| Recipe                  | Does |
| ----------------------- | ---- |
| `bootstrap`             | One-time onboarding: toolchain, hooks, tools, restore. |
| `build`                 | Build the App (x64). |
| `run`                   | Stop → build → launch (unpackaged). |
| `check`                 | Fast inner-loop check (incremental build + fmt-check + strict-code). |
| `watch-check`           | Recompile the App on every change. |
| `fix`                   | Auto-fix whitespace, code style, typos. |
| `fmt` / `fmt-check`     | Apply / verify formatting. |
| `test` / `test-one`     | Run all / a single test project. |
| `watch-test <Project>`  | Re-run a test project on change. |
| `lint`                  | Full static gate (no tests). |
| `analyze`              | Build every project with warnings-as-errors (the heavy gate). |
| `doctor`                | Environment health check. |

## Notes

- A tray-resident instance locks `ClipVault.App.exe`; `just run` / `just stop` (and the VS Code
  `build` task) kill it first.
- Local builds are x64 only — an slnx-wide x64 build hits MSB4126, so recipes build the App and each
  test project individually (see the `analyze` comment in the Justfile).
- `just check` is incremental and App-only for speed. The authoritative gate is `just lint` (and the
  pre-commit `analyze`), which rebuild every project with `--no-incremental` so analyzers always re-run.

## Off-Windows

WinUI requires Windows, so the full app builds and runs on Windows only. `Domain` and `Application`
are `net10.0` (no Windows TFM) and build/test anywhere with the .NET 10 SDK:

```sh
dotnet test tests/ClipVault.Domain.Tests
dotnet test tests/ClipVault.Application.Tests
```

`Infrastructure`, the WinUI `App`, and `just build` / `just run` need Windows. Classifier, capture-rule,
use-case, and docs work are fully doable off-Windows; the Windows gate runs in CI.
