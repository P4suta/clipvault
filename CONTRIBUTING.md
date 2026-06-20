# Contributing to ClipVault

Thanks for your interest. ClipVault is a privacy-first, deliberately minimal Windows clipboard manager;
contributions should keep that focus (read-only-where-possible, no scope creep, no persistent system footprint).

## Prerequisites

- Windows 10 2004+ / Windows 11 (x64).
- [mise](https://mise.jdx.dev/) — pins the toolchain (.NET SDK, `just`, `lefthook`, `typos`, `actionlint`,
  `committed`) from `mise.toml`.

```bash
mise install     # install the pinned toolchain (honors mise.lock)
just bootstrap   # git hooks + local dotnet tools, then restore
```

All `dotnet` calls go through `mise exec -- dotnet`.

## Build, test, and the quality gate

```bash
just build     # build the app (x64)
just test      # run all test projects
just lint      # all static lints: format check + analyzers (warnings as errors) + typos + actionlint + yamllint + markdownlint + strict-code
just ci        # what CI runs: locked restore + lint + test + dependency vulnerability audit
```

`just lint` and `just test` must pass before a PR is merged; `lefthook` runs them on commit/push.
Analyzers are enforced as errors and `TreatWarningsAsErrors` is permanent — fix warnings, do not suppress them
(a suppression needs an attributed `[SuppressMessage(..., Justification = "…")]` and review; `#pragma warning
disable` is rejected by `just strict-code`).

`yamllint` (Python) and `markdownlint-cli2` (Node) are not pinned by mise; their recipes no-op locally with an
install hint and CI enforces them.

Day-to-day commands, the recipe cheat-sheet, and IDE setup live in [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).

## Commit messages

[Conventional Commits](https://www.conventionalcommits.org/). The commit-msg hook (`committed`) enforces the
type prefix; scope is optional. Allowed types: `feat` / `fix` / `docs` / `style` / `refactor` / `perf` /
`test` / `build` / `ci` / `chore` / `revert`. Subjects are lowercase, imperative, and ≤50 chars.

## Conventions

- **Code, comments, and docs are in English**; keep them terse (the essential what/constraint, not narration).
  UI strings and Japanese test fixtures stay localized.
- After changing dependencies (`Directory.Packages.props` / `*.csproj`), run `just relock` and commit the
  updated `packages.lock.json`.
- New GitHub Actions must be SHA-pinned (with a trailing version comment).
- Keep the architecture layered: `Domain` → `Application` → `Infrastructure` → `App` (WinUI). Domain has no
  framework dependencies. See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).
- Record architecturally significant decisions as [ADRs](docs/ADR_INDEX.md) under `docs/adr/`.

## Pull requests

1. Branch from `main`.
2. Keep the change focused; add or update tests.
3. Ensure `just lint` and `just test` are green.
4. Open the PR. Squash-merge is the default, so the **PR title becomes the squashed commit message** — write it
   as a Conventional Commit subject.

## Security

Do **not** report vulnerabilities in public issues or PRs. Use GitHub's private vulnerability reporting; see
[SECURITY.md](SECURITY.md).

By contributing you agree to the [Code of Conduct](CODE_OF_CONDUCT.md) and that your contributions are licensed
under the [Apache License 2.0](LICENSE).
