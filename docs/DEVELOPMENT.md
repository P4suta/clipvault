# Development

Mise pins the toolchain, and the [Justfile](../Justfile) defines the project commands.
Run `just` to list every recipe.
The [contribution guide](../CONTRIBUTING.md) defines contribution policy and the quality gate.
The [architecture guide](ARCHITECTURE.md) maps the project layers.

## Setup

Follow the [source setup](README.md#setup), then open `ClipVault.slnx` in Visual Studio or Rider.
In Visual Studio Code, use the recommended extensions and the tasks in `.vscode/`.
Ctrl+Shift+B builds the app, and F5 starts the unpackaged app under the debugger.
Run `just doctor` to check tool resolution, Git hooks, and the local environment.

## Daily loop

Run `just run` to stop a resident instance, build, and launch the app.
Run `just check` for an incremental app build with style and source checks.
The full gate remains `just lint`.
Run `just watch-check` to rebuild the app when files change.
Run `just fix` to apply formatting and spelling corrections, then inspect the diff.

Run `just test` for every test project or select a project and test filter.
Use `just watch-test Application` to rerun a test project when its files change.

```sh
just test-one Infrastructure
just test-filter KeyProtector
just watch-test Application
```

## Recipe reference

Use `just bootstrap` for the toolchain, hooks, local tools, and dependency restore.
Use `just build` for the x64 app.
Use `just fmt` to format source and `just fmt-check` to verify formatting.
The `just lint` gate includes formatting, analyzers, spelling, workflow syntax, YAML, Markdown, and the source suppression policy.
Run `just pre-commit` to preview the local commit checks.
The `just analyze` recipe rebuilds every project with warnings as errors.

## Build channels

Each build records its channel in `InformationalVersion`, Settings, and `crash.log`.
The [build-channel decision](adr/0008-build-channels-and-release-please.md) defines the version format.

The default `dev` channel uses `X.Y.Z-dev+<sha>` and stores data under `%LOCALAPPDATA%\ClipVault\dev`.
The separate directory keeps development history apart from stable history.
The manually dispatched `nightly` workflow produces an unsigned artifact with 14-day retention and stores data under `%LOCALAPPDATA%\ClipVault\nightly`.
Download its artifact with `gh run download -n clipvault-nightly`.
The `stable` channel uses `X.Y.Z` and stores data under `%LOCALAPPDATA%\ClipVault`.

The `release-please` workflow updates `<Version>` in `Directory.Build.props` from Conventional Commits.
Run `just version [channel]` to print the version string.

## Build behavior

A resident instance holds `ClipVault.App.exe` open.
The `just run`, `just stop`, and Visual Studio Code build tasks stop it before building.
Local app builds target x64.
Recipes build the app and each test project because a solution-wide x64 build reports MSB4126.

The `just check` recipe uses an incremental app build for the inner loop.
The full `just lint` gate invokes `just analyze` with `--no-incremental` so analyzers run for every project.

## Work outside Windows

WinUI requires Windows for the app build and launch.
The `ClipVault.Domain` and `ClipVault.Application` projects target `net10.0` and can run their tests on other systems with the .NET 10 SDK.

```sh
dotnet test tests/ClipVault.Domain.Tests
dotnet test tests/ClipVault.Application.Tests
```

Infrastructure, the WinUI app, `just build`, and `just run` require Windows.
Classifier, capture-rule, use-case, and documentation changes can use the portable projects.
CI runs the Windows gate.
