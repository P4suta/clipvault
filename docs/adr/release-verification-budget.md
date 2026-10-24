---
date: 2026-10-24
---

# Release verification budget

## Status

Accepted.
This supersedes the daily nightly schedule in [Build channels and automated versioning](0008-build-channels-and-release-please.md).

## Context

Pull requests run the full Windows formatting, analyzer, test, coverage, and dependency audit gate.
Separate fuzz and CodeQL jobs repeat expensive work for each pull request and on schedules.
Daily preview builds also consume runners without a requested preview.

## Decision

Keep the Windows gate on pull requests and merge groups.
Run bounded fuzz and CodeQL jobs for the selected release source before distribution building.
Use isolated jobs with read-only repository access and no restored build caches.
Keep signing and publication behind the existing approval gates.
Provide a verification-only mode that skips distribution building, signing, and publication.
Build unsigned previews only on manual request.

## Consequences

Pull requests retain deterministic validation without automatic fuzz campaigns or separate CodeQL builds.
Release preparation waits for both source verification jobs.
CodeQL results remain available as a release workflow artifact.
Parser fuzzing still detects crashes before distribution building.

## Alternatives considered

Keeping recurring campaigns repeats work without selecting a distribution candidate.
Removing the verification tools would lose checks for release candidates.

## References

- [Release workflow](../../.github/workflows/release.yml)
- [Manual preview workflow](../../.github/workflows/nightly.yml)
