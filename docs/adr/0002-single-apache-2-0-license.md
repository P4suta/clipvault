# 2. License under Apache-2.0 only

- Status: accepted
- Date: 2026-06-18
- Deciders: project maintainers
- Tags: process, licensing

## Context

ClipVault is scaffolded to match the conventions of
[project-template](https://github.com/P4suta/project-template), whose
`core` layer ships projects dual-licensed under **Apache-2.0 OR MIT**.
ClipVault, however, had already settled on a single **Apache-2.0**
license before adopting the template's conventions, and ships a patent
grant and explicit `NOTICE` attribution that the team wants to keep as
the sole governing terms.

## Decision

License ClipVault under **Apache-2.0 only**. Diverge from the
template's dual-license default: keep a single `LICENSE` (Apache-2.0),
set `PackageLicenseExpression` to `Apache-2.0`, and keep `NOTICE`
scoped to that single license. Do not add `LICENSE-MIT`.

## Consequences

* One unambiguous license and patent grant for all consumers; no
  "at your option" choice to reason about.
* A deliberate, documented divergence from the template — future
  syncs with the template must not reintroduce `LICENSE-MIT` or a
  dual-license SPDX expression.
* Contributors agree to Apache-2.0 (not the template's dual terms);
  `CONTRIBUTING.md` states this.

## Alternatives considered

* **Adopt the template's dual Apache-2.0 OR MIT.** Rejected: maximises
  downstream permissiveness but reopens a settled decision and dilutes
  the single, patent-granting license the project intends to ship.
* **MIT only.** Rejected: drops the explicit patent grant that
  Apache-2.0 provides.

## References

- Apache License 2.0, <https://www.apache.org/licenses/LICENSE-2.0>.
- project-template `core` layer license model.
