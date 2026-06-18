# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/).

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
