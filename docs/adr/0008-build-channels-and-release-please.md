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
- **Versioning (draft-first, dispatch-only).** release-please reads Conventional Commits on
  `main` and maintains a Release PR (version + changelog). Merging materializes the `v<X.Y.Z>`
  tag (`force-tag-creation`) and creates the GitHub Release as a **draft** (`draft: true`).
  `release-please.yml` then dispatches `release.yml` (`workflow_dispatch`, via `gh workflow
  run`), which builds → signs → attaches the assets to the draft → **publishes it**. Assets
  land before publish — the order [immutable releases](https://docs.github.com/code-security/concepts/supply-chain-security/immutable-releases)
  require (a published release can't gain assets afterward). `release.yml` is triggered *only*
  by that dispatch, never by a tag push, so a stray `v*` tag starts nothing. `force-tag-creation`
  is needed because a draft release otherwise carries no git tag, so release-please can't see the
  just-cut release and would open a spurious "next" Release PR re-listing shipped commits
  (googleapis/release-please#1650).
- **Nightly.** `nightly.yml` builds an unsigned bundle from `main` daily, stamped
  `nightly.<date>`, published as a 14-day Actions artifact (not a Release). Skips when
  `main` is unchanged in 24h. Signing stays stable-only (ADR-0004 trust model unchanged).

## Consequences

- Dev/nightly builds are fully isolated from released data and self-identify (crash.log,
  Settings). Replaces the prior "git tag is the only version" policy.
- Conventional Commits become load-bearing (already enforced via `committed`/lefthook).
- App credentials `RELEASE_PLEASE_CLIENT_ID` / `RELEASE_PLEASE_PRIVATE_KEY` (the App is
  authenticated by **Client ID**), scoped to a dedicated `release-please` environment (no required
  reviewers — the job runs on every push to `main`; the environment is the secret boundary,
  restricted to the `main` branch). The `release.yml` dispatch uses `GITHUB_TOKEN` (a
  `workflow_dispatch` is the one event it may start).
- Branch protection on `main` is enforced via a repository **ruleset** (`main protection`), not the
  legacy branch-protection rules.
- **Release safety, defence in depth** (see [RELEASING.md](../RELEASING.md)): a required
  `release-gate` check keeps the Release PR unmergeable until the `release: approved` label is
  applied (passes immediately on non-release PRs); `release-label-guard.yml` restores the
  `autorelease: pending` tracking label if removed from an *open* release PR; `no-automerge-on-release-pr.yml`
  disables auto-merge if armed on a release PR (deliberate manual merge); both the `sign` and
  `publish` jobs pause on the approval-gated `release` environment. Governance labels are declared
  in `.github/labels.json` and synced by `labels-sync.yml`.
- The old tag **ruleset** (restrict `v*` create to the release-please App) was **retired**: since
  `release.yml` is dispatch-only, a stray `v*` tag no longer triggers a release — each real release's
  tag↔commit↔assets binding is sealed by the immutable-release attestation instead. Only the
  `main protection` branch ruleset remains.

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
- `.github/workflows/{release-please,nightly,release,release-gate,release-label-guard,no-automerge-on-release-pr,labels-sync}.yml`,
  `.github/actions/verify-signatures/action.yml`, `.github/labels.json`,
  `release-please-config.json`, `.release-please-manifest.json`.
- [RELEASING.md](../RELEASING.md), [SIGNING.md](../SIGNING.md),
  [VERIFICATION.md](../VERIFICATION.md), [DEVELOPMENT.md](../DEVELOPMENT.md).
