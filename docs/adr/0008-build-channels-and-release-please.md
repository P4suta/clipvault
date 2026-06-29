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
- New secrets `RELEASE_PLEASE_APP_ID` / `RELEASE_PLEASE_APP_PRIVATE_KEY` (dedicated App).
- Recommended repo hardening (settings, out of band): require a `release: approved` label
  check on the Release PR, and restrict `v*.*.*` tag creation to the release-please App.

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
