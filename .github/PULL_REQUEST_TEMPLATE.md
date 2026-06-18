<!-- The PR title becomes the squashed commit message — write it as a Conventional Commit subject
     (e.g. `feat: add per-app exclusion`). For security issues, use private vulnerability reporting,
     not a public PR. -->

## Summary

<!-- What does this change, and why? Link any related issue. -->

## Verification

- [ ] `just lint` passes (format + analyzers as errors + typos + actionlint/yamllint/markdownlint + strict-code).
- [ ] `just test` passes; tests added or updated for the change.
- [ ] No new warnings; no analyzer suppressions without an attributed `Justification`.
- [ ] Dependency changes are accompanied by a regenerated `packages.lock.json` (`just relock`).
- [ ] Docs/comments updated where relevant (English, terse).

## ADR

<!-- If this makes an architecturally significant decision, link the ADR (docs/adr/NNNN-*.md). -->
