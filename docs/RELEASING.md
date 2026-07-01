# Releasing

ClipVault versions itself from Conventional Commits — no manual version bump. This page covers how a
release happens, how to **activate** the automation, the first-release bootstrap, and the **nightly**
channel. Rationale: [ADR-0008](adr/0008-build-channels-and-release-please.md). Signing:
[SIGNING.md](SIGNING.md). Download verification: [VERIFICATION.md](VERIFICATION.md).

## How a release happens (once activated)

1. Conventional Commits land on `main` (squash-merged PRs; the PR title is the commit). `feat:` → minor,
   `fix:`/`perf:` → patch, `!` / `BREAKING CHANGE:` → major.
2. [`release-please`](../.github/workflows/release-please.yml) keeps a **Release PR** open that bumps
   `<Version>` in [`Directory.Build.props`](../Directory.Build.props) (the `x-release-please-version`
   marker line, via the `generic` extra-file updater) and `.release-please-manifest.json`, and updates
   [`CHANGELOG.md`](../CHANGELOG.md).
3. **Add the `release: approved` label** to the Release PR. Until it's there the `release-gate` check
   fails the PR, so a release is never an accidental merge.
4. **Merge the Release PR by hand** (auto-merge is disabled on release PRs by
   `no-automerge-on-release-pr.yml`). release-please creates the GitHub Release as a **draft**
   (`draft: true`) and **materializes the `vX.Y.Z` git tag** at the release commit
   (`force-tag-creation: true`), then dispatches [`release.yml`](../.github/workflows/release.yml).
5. `release.yml` runs: build → **sign (approve in the `release` environment)** → **publish (approve
   again)**. The publish step attaches the signed bundle + symbols + SBOM + `SHA256SUMS.txt` to the
   draft (which already carries the tag from step 4) and **publishes it**. Assets land *before* publish,
   the order [immutable releases](https://docs.github.com/code-security/concepts/supply-chain-security/immutable-releases)
   require — a published release can't gain assets afterward.

You never hand-pick or hand-edit a version. The Release PR diff *is* the preview.

## Release safety (defence in depth)

Cutting a real, immutable release is gated by several independent steps (ADR-0008), so an ambiguous
instruction can't ship one by accident:

- **Label gate** — the Release PR can't merge until `release: approved` is added (`release-gate`).
  Adding/removing the label re-evaluates the gate automatically (CI runs on `labeled`/`unlabeled`).
- **Label guard** — `autorelease: pending` is release-please's tracking label; without it the merged PR
  is never tagged/released. `release-label-guard.yml` reinstates it if removed from an **open** release
  PR. The human `release: approved` label is deliberately *not* guarded (un-approving is legitimate).
- **Manual merge** — `no-automerge-on-release-pr.yml` turns auto-merge back off if armed on a release PR.
- **No tag-triggered cascade** — `release.yml` is started only by the explicit dispatch from
  `release-please.yml`, never by a tag push, so a stray `vX.Y.Z` tag starts nothing.
- **Two environment approvals** — both `sign` and `publish` pause on the `release` environment; the
  irreversible publish has its own approval.
- **Agent contract** — automated tooling (incl. the AI assistant) will not merge the Release PR, push a
  `v*` tag, approve the `release` environment, or run `release.yml` with `publish=true` without an
  explicit, version-named instruction.

Labels are declared in `.github/labels.json` (synced by `labels-sync.yml`), so their names/colors/meaning
are version-controlled, not ad-hoc.

## Activation (one-time)

release-please ships **dormant**: with the App secrets unset, `release-please.yml` runs and no-ops. It
runs as a **GitHub App** because a tag pushed by the default `GITHUB_TOKEN` does not trigger further
workflows (GitHub's recursion guard). The workflow mints a short-lived installation token at runtime via
`actions/create-github-app-token`.

1. **Create a GitHub App** (org or personal). Repository permissions: **Contents: Read & write** and
   **Pull requests: Read & write**. No webhook needed.
2. **Install** the App on the `clipvault` repo.
3. Generate a **private key** (`.pem`) and note the App's **Client ID** (App → General settings, e.g. `Iv…`).
4. **Create an environment** for the credential: Settings → Environments → **New environment** →
   **`release-please`**.
   - **Deployment branches and tags** → **Selected branches** → add **`main`** only.
   - **Do NOT add required reviewers** (release-please must run unattended).
5. In that environment's **Environment secrets**, add:
   - `RELEASE_PLEASE_CLIENT_ID` = the App's Client ID
   - `RELEASE_PLEASE_PRIVATE_KEY` = the full `.pem` contents (`-----BEGIN…` through `…END-----`)
6. Add `softprops/action-gh-release`, `actions/attest`, and `sslcom/esigner-codesign` (the SHA-pinned
   versions used in `release.yml`) to the repo's Actions allowlist if "selected actions" is enforced.

### Why an environment, not a repository secret

A **repository** secret is readable by a workflow run on *any* branch. The App private key carries
`contents: write` + `pull-requests: write`, so we scope it: the job declares `environment: release-please`,
and the environment's `main`-only branch policy means only the main-branch run can read the key. This
mirrors the signing secrets in the `release` environment, with one difference: **release-please's
environment has no required reviewers** (the human gate is merging the Release PR; the signing approval
gate is separate).

> A fine-grained **PAT** with the same two permissions also works — drop the `create-github-app-token`
> step and pass the PAT as `token:`. The App is preferred (no human-tied credential; short-lived, repo-scoped).

## First release (bootstrap to v0.1.0)

The first release is pinned to **`0.1.0`** by a one-time `Release-As: 0.1.0` footer already committed
(`chore: cut first release as 0.1.0`), with the manifest seeded to `0.0.0`. Without the pin, release-please
treats a first release from a `0.0.0` manifest as `1.0.0` — wrong for a pre-1.0 project. Because the pin is
a commit footer (not `release-as` in the config), it is consumed once when v0.1.0 is cut and needs no
later cleanup; subsequent releases derive normally from Conventional Commits.

On the first Release PR, confirm the diff bumps **both** `<Version>` in `Directory.Build.props` (the
`x-release-please-version` line) and `CHANGELOG.md`, and that the proposed version is **`0.1.0`** (not
`1.0.0`). `.release-please-manifest.json` flips from `0.0.0` to the released version at merge.

## Nightly builds

[`nightly.yml`](../.github/workflows/nightly.yml) builds an **unsigned** bundle from the tip of `main`
daily (and on demand), stamped `X.Y.Z-nightly.<date>`, published as a **14-day GitHub Actions artifact**,
not a Release — keeping it off the Releases list and clear of the immutable-release flow. Skips when `main`
is unchanged in 24h. Grab the latest:

```bash
gh run download --repo P4suta/clipvault -n clipvault-nightly
```

Nightlies are unsigned (SmartScreen will warn), carry no stability guarantee, and isolate their own data
under `%LOCALAPPDATA%\ClipVault\nightly`.

## Build identity (channels)

The base `X.Y.Z` is the release-please-managed number in `Directory.Build.props`; the channel suffix is
layered at build time via the MSBuild `Channel` property, driving `InformationalVersion`.

| Channel | Example | Where |
|---|---|---|
| dev | `0.1.0-dev+g<sha>` | local `just publish` / `dotnet build` |
| nightly | `0.1.0-nightly.20260701` | `nightly.yml` |
| stable | `0.1.0` | `release.yml` (release commit; version from the tag) |
