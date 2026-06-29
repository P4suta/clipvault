# 8. Build channels and automated versioning

- Status: accepted
- Date: 2026-06-30
- Deciders: project maintainers
- Tags: build, release, ci

## Context

Before this decision the version came only from the git tag, injected at release time.
A development build was indistinguishable from a release at runtime and shared the
released build's data directory (`%LOCALAPPDATA%\ClipVault`), so a dev build could read
or overwrite real clipboard history and the encryption key. There was also no way to
ship a build between releases for testing.

## Decision

Adopt three build channels stamped at compile time, plus release-please for versioning.

- **Channels.** The MSBuild `Channel` property (default `dev`; CI overrides to
  `nightly.<date>` or `stable`) drives `InformationalVersion`: `X.Y.Z-dev`,
  `X.Y.Z-nightly.<date>`, or `X.Y.Z`. Source Link appends `+<sha>`. `BuildInfo` parses
  this once at runtime; `AppPaths` derives a per-channel data root and channel-qualified
  OS identifiers, so non-stable builds never touch stable data, startup registration, or
  the Windows Hello credential.
- **Base version source of truth.** `<Version>` in `Directory.Build.props` (marked
  `x-release-please-version`), bumped by release-please together with the manifest.
- **Versioning.** release-please reads Conventional Commits on `main` and maintains a
  Release PR (version + changelog). Merging tags `v<X.Y.Z>` and creates the Release. A
  dedicated GitHub App token lets that tag trigger `release.yml`, which builds, signs,
  attests, and uploads the assets onto the release.
- **Nightly.** `nightly.yml` builds an unsigned bundle from `main` daily, stamped
  `nightly.<date>`, published as a 14-day Actions artifact (not a Release). Skips when
  `main` is unchanged in 24h. Signing stays stable-only (ADR-0004 trust model unchanged).

## Consequences

- Dev/nightly builds are fully isolated from released data and self-identify (crash.log,
  Settings). Replaces the prior "git tag is the only version" policy.
- Conventional Commits become load-bearing (already enforced via `committed`/lefthook).
- New App credentials `RELEASE_PLEASE_APP_ID` / `RELEASE_PLEASE_APP_PRIVATE_KEY`, scoped to a
  dedicated `release-please` environment (no required reviewers — the job runs on every push to
  `main`; the environment is the secret boundary, restricted to the `main` branch).
- Branch protection on `main` is enforced via a repository **ruleset** (`main protection`), not the
  legacy branch-protection rules.
- A required `release-gate` check (`.github/workflows/release-gate.yml`) keeps the Release PR
  unmergeable until the `release: approved` label is applied; it passes immediately on non-release PRs.
- A tag **ruleset** restricts `v*` tag create/update/delete to the release-please GitHub App (its
  Integration ID is the sole bypass actor), so only an approved release can mint a release tag.

## Alternatives considered

- **Git-derived versioning (MinVer / Nerdbank.GitVersioning).** Rejected: breaks on
  `.git`-less builds and does not map Conventional Commits to SemVer bumps.
- **Keep the bespoke tag-only release.** Rejected: no automated bump/changelog and no
  nightly; the maintainer hand-picks every version.
- **Rolling `nightly` Release tag.** Rejected: incompatible with immutable releases and
  pollutes the Releases list.
- **CalVer.** Rejected: SemVer + Conventional Commits is the mainstream pairing.

## References

- `Directory.Build.props`, `src/ClipVault.Infrastructure/Diagnostics/BuildInfo.cs`,
  `src/ClipVault.Infrastructure/AppPaths.cs`.
- `.github/workflows/{release-please,nightly,release}.yml`,
  `release-please-config.json`, `.release-please-manifest.json`.
- [VERIFICATION.md](../VERIFICATION.md), [DEVELOPMENT.md](../DEVELOPMENT.md).
