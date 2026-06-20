# 4. Encrypt clipboard history at rest

- Status: accepted
- Date: 2026-06-17
- Deciders: project maintainers
- Tags: architecture, security

## Context

Clipboard history routinely contains secrets — passwords, tokens, personal data.
Persisted history must stay confidential against offline disk access, and the data
encryption key (DEK) must be sealed to the user, not stored in the clear. The key
file must also resist tampering and downgrade. See [SECURITY.md](../../SECURITY.md).

## Decision

Encrypt every sensitive field (content, preview, thumbnail, source) with
ChaCha20-Poly1305 AEAD (`ChaCha20Poly1305EncryptionService`); only non-sensitive
metadata (type, timestamps, pin state) stays plaintext. Subkeys are separated with
HKDF-SHA256; the ciphertext format is `[version | nonce | tag | ciphertext]`. The DEK
is sealed with DPAPI (CurrentUser) and may add a second factor — an Argon2id-derived
passphrase key or Windows Hello. The key file (`KeyProtector`) carries an
authenticated header (magic `CVK1`, `FormatVersion = 2`, mode) so a tampered or
downgraded header fails closed. SQLite uses `PRAGMA secure_delete=ON`.

## Consequences

- Confidentiality at rest; destroying the DEK is an effective crypto-erase of all
  history.
- Key management is non-trivial: DPAPI, passphrase (Argon2id), and Hello paths each
  need their own unlock flow.
- The authenticated header bumped the format to version 2 — pre-v2 key files must be
  recreated (the old `dek.bin` is not upgraded in place).
- `secure_delete=ON` adds write cost, accepted for the deletion guarantee.

## Alternatives considered

- **AES-GCM.** Rejected: ChaCha20-Poly1305 is the chosen BCL AEAD and is constant-time
  in software without depending on AES-NI; security is comparable.
- **Plaintext + filesystem ACL.** Rejected: ACLs give no protection against offline
  disk access or another process running as the same user.
- **OS Credential Locker for the whole history.** Rejected: it is sized for small
  secrets, not bulk content; DPAPI is used only to seal the DEK.

## References

- [SECURITY.md](../../SECURITY.md) — threat model and crypto details.
- `src/ClipVault.Infrastructure/Security/KeyProtector.cs`,
  `ChaCha20Poly1305EncryptionService.cs`.
