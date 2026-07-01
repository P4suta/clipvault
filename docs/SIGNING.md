# Code Signing — Authenticode Signing of Distributables

Runbook for Authenticode-signing ClipVault's own binaries (`ClipVault.exe` launcher +
`app\ClipVault.App.exe` app body) in the distribution bundle with **SSL.com eSigner**, via the official
**`sslcom/esigner-codesign` Action** (`command: batch_sign`). Pipeline rationale:
[ADR-0008](adr/0008-build-channels-and-release-please.md). Release flow: [RELEASING.md](RELEASING.md).
User-side download verification: [VERIFICATION.md](VERIFICATION.md).

## Current state

**Dormant (scaffolded).** No code-signing certificate is configured yet, so every release ships
**unsigned but with cryptographic provenance** (Sigstore-backed attestations — see VERIFICATION.md). The
signing path in `release.yml` is fully wired and activates automatically once the `ES_*` secrets exist in
the `release` environment; no code change is needed to turn it on. This page is the activation +
renewal runbook.

Only ClipVault's **own two PE files** are signed — the root launcher `ClipVault.exe` (what the user
double-clicks = the main SmartScreen target) and the app body `app\ClipVault.App.exe`. The bundled .NET /
WindowsAppSDK runtime DLLs are already Microsoft-signed and are not re-signed (avoids wasting quota and
signing others' works).

## How it works (pipeline shape)

`release.yml` runs three jobs so the signing credentials touch the smallest surface (ADR-0008):

1. **build** — assembles the `ClipVault/` bundle (app body + launcher) + SBOM, uploads them as artifacts.
   **No secrets**, read-only token.
2. **sign** — downloads the bundle, stages the two PEs into a flat `sign-stage\`, runs the
   **`sslcom/esigner-codesign` Action** (`batch_sign`: CodeSignTool scans then signs, RFC 3161 timestamped
   via SSL.com's TSA) into `signed\`, copies the signed files back, then hard-verifies. **The only job that
   sees the signing secrets**, so it runs in the approval-gated **`release` environment**.
3. **publish** — downloads the signed bundle, **re-verifies it is signed when signing is configured** (a
   gate at the irreversible boundary — the same chain + timestamp + signer check the sign job runs, via the
   shared [`verify-signatures`](../.github/actions/verify-signatures/action.yml) composite). While signing
   is dormant (`HAVE_SIGNING=false`) the gate is skipped and the unsigned-with-provenance bundle publishes
   with a `::warning::` (ClipVault's intended trust model); once the `ES_*` secrets exist the gate is hard
   and a nominally-signed-but-not bundle fails before the Release. Then zips + checksums it, writes keyless
   attestations (OIDC, no secrets), attaches everything to the release-please draft, and publishes it.
   Gated on `publish=true`.

Verification enforces a valid chain, an RFC 3161 timestamp, and the expected signer:

```text
signtool verify /pa /tw <file>     # exit 0 = chain valid + timestamped; 2 = no timestamp; 1 = invalid
Get-AuthenticodeSignature <file>   # SignerCertificate.Subject must contain CN=Yasunobu Sakashita
```

## Activation procedure

### A. Obtain the certificate (SSL.com)

1. Create an account at [SSL.com](https://www.ssl.com/).
2. Purchase a **Code Signing** certificate with **Individual Validation (IV) + eSigner (cloud signing)**
   support (the cloud version, not the USB-token version).
3. Complete IV identity verification (government ID; no corporate registration required).

### B. Configure eSigner for automated signing

4. In the SSL.com dashboard, on the certificate order: set the eSigner signing secret and enable automated
   signing; note the **Credential ID**, the **TOTP (2FA) secret** (Base32), and the account
   **username / password**.

### C. Register the signing secrets in the `release` environment

The secrets live in an **approval-gated GitHub Environment**, not at repository level, so a
`workflow_dispatch` from an arbitrary ref (or a compromised workflow) cannot mint signatures unattended.

5. Repository → **Settings → Environments → New environment** → name it **`release`**.
6. **Required reviewers**: add yourself (every signing run pauses for a deliberate approval).
   **Deployment branches and tags → Selected**: allow `main` (release.yml always runs via
   `workflow_dispatch --ref main`).
7. **Environment secrets** — all four are required by `batch_sign`:

   | Secret name | Value |
   |---|---|
   | `ES_USERNAME` | SSL.com username |
   | `ES_PASSWORD` | SSL.com password |
   | `ES_CREDENTIAL_ID` | Credential ID of the signing certificate |
   | `ES_TOTP_SECRET` | TOTP secret for eSigner automated signing (Base32) |

   If any exist as **repository** secrets, delete the repository copies — leaving them at repo level defeats
   the environment isolation. Once `ES_USERNAME` + `ES_CREDENTIAL_ID` are present, `HAVE_SIGNING` becomes
   `true` and the `sign` job signs (after approval).

### D. Verification

8. **Signing smoke test (safe under immutable releases)**: Actions → release → `workflow_dispatch` with
   `tag_name=main`, `publish=false`. The `build` job runs, then the **`sign` job pauses for approval**.
   Approve it; confirm **Verify signatures** prints `verified: … - CN=Yasunobu Sakashita
   (chain+timestamp+signer OK)` for both files and the run **ends cleanly after `sign`** (no Release).
9. **Real release**: merge the `release: approved` Release PR — release-please creates the draft and
   dispatches release.yml. After both approvals, `build`→`sign`→`publish` runs end-to-end and the signed
   zip + symbols + SBOM + `SHA256SUMS.txt` + attestations are attached to the published release.
10. **Local confirmation**: extract the Release zip and on Windows:
    ```powershell
    signtool verify /pa /tw /v ClipVault.exe          # → Successfully verified, with a timestamp
    Get-AuthenticodeSignature ClipVault.exe            # → Status: Valid
    Get-AuthenticodeSignature app\ClipVault.App.exe    # the app body → Status: Valid
    ```

## Renewal (handling expiry)

- A publicly trusted code-signing certificate is valid for **at most ~460 days** (CA/Browser Forum). Renew
  at SSL.com before expiry.
- Update the corresponding Environment secret **only if the Credential ID / TOTP change** on renewal. If the
  certificate **subject** ever changes, update `SIGNER_SUBJECT_CONTAINS` in `release.yml` too — it is
  asserted at verify time.

## Troubleshooting

- **`sign` job never starts**: waiting on the `release` environment's required reviewer — approve the run.
- **`hash needs to be scanned first before submitting for signing`**: the SSL.com account has the pre-signing
  malware blocker enabled, so `batch_sign` can't sign unscanned hashes. Already wired: `malware_block: "true"`
  scans inline before signing. (Only flip to `false` for MSIX inputs, which SSL.com requires be unscanned.)
- **Verify fails with `NotSigned`**: `batch_sign` does not honor `override`, so signed files must be written
  to an explicit `output_path`, else copy-back grabs the unsigned originals. Already wired (`output_path: …\signed`).
- **`verify failed … chain invalid or NOT timestamped`**: `signtool verify /pa /tw` returned non-zero — the
  SSL.com TSA may have been unreachable during signing, or the chain didn't resolve. Re-run; a timestamp is
  required so signatures survive cert expiry.
- **`unexpected signer …`**: the signed cert subject doesn't contain `SIGNER_SUBJECT_CONTAINS` (renewal under
  a new name?). Update the env var in `release.yml`.
- **A SmartScreen warning still appears on first launch**: expected (reputation is shallow). It fades as
  downloads accumulate. Same with EV — and since March 2024 EV no longer grants immediate SmartScreen trust,
  so individual-name **IV** is sufficient for an app with no kernel driver.

## Related

- [ADR-0008 — Build channels and automated versioning](adr/0008-build-channels-and-release-please.md)
- [RELEASING.md](RELEASING.md), [VERIFICATION.md](VERIFICATION.md)
- Wiring: `.github/workflows/release.yml`, `.github/actions/verify-signatures/action.yml`
