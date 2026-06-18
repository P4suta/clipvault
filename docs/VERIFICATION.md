# Verifying a ClipVault release

Every ClipVault release is published with cryptographic **build provenance** and an attested **SBOM**, so you
can confirm a downloaded binary was built by this repository's release workflow from this source — *even
though the binary is not yet Authenticode code-signed*. This is the project's current trust model:
**unsigned, but independently verifiable**.

## What you need

- [GitHub CLI](https://cli.github.com/) (`gh`) version 2.x or newer, authenticated (`gh auth login`).
- The release assets from the GitHub Releases page:
  - `ClipVault-<version>-win-x64.zip` — the application
  - `ClipVault-<version>-win-x64-symbols.zip` — debug symbols (PDBs)
  - `clipvault.cdx.json` — the CycloneDX SBOM
  - `SHA256SUMS.txt` — checksums of all assets

## 1. Verify the checksums

```bash
# In the folder containing the downloaded assets and SHA256SUMS.txt:
sha256sum -c SHA256SUMS.txt
```

All listed files must report `OK`.

## 2. Verify build provenance (SLSA)

Provenance is bound to both the distributed `.zip` and the `ClipVault.App.exe` inside it. Verify the artifact
you actually downloaded:

```bash
gh attestation verify ClipVault-<version>-win-x64.zip --repo P4suta/clipvault
```

For a stricter check, pin the exact workflow identity that is allowed to produce releases:

```bash
gh attestation verify ClipVault-<version>-win-x64.zip \
  --repo P4suta/clipvault \
  --signer-workflow P4suta/clipvault/.github/workflows/release.yml
```

You can also verify the unpacked executable directly:

```bash
gh attestation verify ClipVault.App.exe --repo P4suta/clipvault
```

A successful run prints the verified provenance predicate (the workflow, commit, and runner that built it).
Verification validates the Sigstore signature and that the artifact's digest matches the attestation — this is
the "keyless signing" guarantee that holds with no Authenticode certificate.

**SLSA Build Level.** Releases are built on GitHub-hosted, ephemeral runners, and the provenance is generated
and signed by GitHub's attestation service — not by the build itself — and bound to the `release.yml` workflow
identity. This meets **SLSA Build Level 2**, with the Level-3 property that provenance is produced by the build
platform and is non-falsifiable by the build; the `--signer-workflow` check above is what enforces that identity
binding.

**Reproducibility.** Compilation is deterministic (`Deterministic` + `ContinuousIntegrationBuild`, portable
PDBs, embedded source). The self-contained publish is **not** intended to be bit-for-bit reproducible — it runs
crossgen (ReadyToRun) and bundles the .NET/Windows App SDK runtime, which vary by SDK/runtime patch. Provenance
(not byte-identical rebuilds) is the integrity guarantee for the shipped binary.

## 3. Inspect / verify the SBOM

The CycloneDX SBOM is attested against the binary's digest:

```bash
gh attestation verify ClipVault.App.exe \
  --repo P4suta/clipvault \
  --predicate-type https://cyclonedx.org/bom
```

`clipvault.cdx.json` lists every component (and version) that went into the shipped closure. Open it with any
CycloneDX-aware tool, or scan it for known vulnerabilities (e.g. `grype sbom:clipvault.cdx.json`).

## 4. (After code signing) Verify the Authenticode signature

Once releases are signed with the SSL.com certificate, the `.exe` will additionally carry an Authenticode
signature with an RFC-3161 timestamp. On Windows:

```powershell
signtool verify /pa /v ClipVault.App.exe
```

Until then, the binary is intentionally unsigned and Windows SmartScreen may warn on first run; the provenance
and SBOM attestations above are the cryptographic guarantee of origin and integrity in the meantime.
