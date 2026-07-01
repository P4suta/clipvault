# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/).

## 0.1.0 (2026-07-01)


### Features

* **app:** log startup failures to crash.log ([4578da1](https://github.com/P4suta/clipvault/commit/4578da11d9b5eea4fd4ca8e2ef6bb0bda11cf8b4))
* bound memory by viewport, not history size ([2d97630](https://github.com/P4suta/clipvault/commit/2d976305ff41a00ee2f556e2c5c7e2a72e59c7d4))
* bound memory by viewport, not history size ([047da87](https://github.com/P4suta/clipvault/commit/047da87b4a209ef6bffb96e4dee57090407f17e6))
* **build:** dev/nightly/stable channels, nightly & release-please ([#19](https://github.com/P4suta/clipvault/issues/19)) ([e229e7b](https://github.com/P4suta/clipvault/commit/e229e7b67d0f18568241aef8a70124e92fe746c0))
* Fluent Design redesign with expanded test suite ([cf9272b](https://github.com/P4suta/clipvault/commit/cf9272baca0e13b8e9a09f4af22a324461ae3df7))
* redesign UI to Fluent Design ([af7e62b](https://github.com/P4suta/clipvault/commit/af7e62bc83c27f616524e65bfc5ebf1ef4cabdce))
* **release:** ship a ClipVault/ folder bundle ([#18](https://github.com/P4suta/clipvault/issues/18)) ([0ab2cc7](https://github.com/P4suta/clipvault/commit/0ab2cc72c338358059e08e07dfb7cc8729cc73e0))


### Bug Fixes

* **app:** bundle app PRI into publish output ([676821c](https://github.com/P4suta/clipvault/commit/676821ceb76acacf0b2eb22f81569f514a159178))
* **ci:** disable yamllint new-lines rule for CRLF ([5057686](https://github.com/P4suta/clipvault/commit/5057686381be02535fb7fb7bf45668e0f7120483))
* **ci:** exclude CHANGELOG.md from markdownlint ([#26](https://github.com/P4suta/clipvault/issues/26)) ([66c8466](https://github.com/P4suta/clipvault/commit/66c8466c546ffbf559b4246a1cc348bfc75fd5ee))
* **ci:** lock actionlint URLs in mise.lock ([c1bdcbb](https://github.com/P4suta/clipvault/commit/c1bdcbb7a91a7436eb8bc9aec6244e430aeef955))
* **release:** fall back to 0.0.0 for smoke build ([#25](https://github.com/P4suta/clipvault/issues/25)) ([31ee0d7](https://github.com/P4suta/clipvault/commit/31ee0d79e216598ad72bdb42caca4f1a06d79172))


### Miscellaneous Chores

* cut first release as 0.1.0 ([#22](https://github.com/P4suta/clipvault/issues/22)) ([0855316](https://github.com/P4suta/clipvault/commit/0855316720f4c79953a4c97d3aff478b1eaefd90))

## [Unreleased]

### Added

- Encrypted-at-rest clipboard history: each field is sealed with
  ChaCha20-Poly1305 (AEAD); the master key is sealed with Windows DPAPI
  and can be further protected by a passphrase (Argon2id) or Windows Hello.
- Volatile mode: keep the entire history in RAM only, never touching disk.
- Privacy gate: secrets (API keys, JWTs, PEM private keys, credit-card
  numbers, passwords) are rejected or masked before storage; the OS
  "exclude from clipboard history" signal is honored; per-application
  exclusion is supported.
- Lightweight, install-free distribution: an unpackaged self-contained
  executable, tray-resident, summoned with a global hotkey; OS
  registrations are runtime-only and released on exit.
- Release integrity: SLSA build provenance and an attested CycloneDX SBOM,
  both bound to the binary's digest, verifiable with `gh attestation verify`.
