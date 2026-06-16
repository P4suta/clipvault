using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using ClipVault.Application.Abstractions;
using Konscious.Security.Cryptography;

namespace ClipVault.Infrastructure.Security;

/// <summary>
/// Protects the master key (DEK) on disk: always DPAPI (CurrentUser), optionally with a second factor
/// via passphrase (Argon2id) or Windows Hello. The second factor blocks decryption even by malware
/// running as the same user; deleting the key file crypto-erases the vault.
///
/// File format: [magic "CVK1"(4) | version(1) | mode(1) | DPAPI(body)]
///   mode 0 (DPAPI):      body = DEK(32)
///   mode 1 (passphrase): body = [salt(16)|memKiB(4)|iters(4)|par(4)|nonce(12)|tag(16)|wrappedDEK(32)]
///   mode 2 (Hello):      body = [challenge(32)|nonce(12)|tag(16)|wrappedDEK(32)]
/// The body (including the Argon2id parameters) is wrapped with ChaCha20-Poly1305 inside the DPAPI
/// envelope, so it cannot be downgraded; only the frame header (magic|version|mode) is unauthenticated.
/// </summary>
/// <param name="keyFilePath">The path to the key file that stores the protected DEK.</param>
/// <param name="argon">The Argon2id cost parameters to use for passphrase protection, or <see langword="null"/> to use the secure defaults.</param>
public sealed class KeyProtector(string keyFilePath, Argon2Parameters? argon = null)
{
    private const int DekSize = 32;
    private const int SaltSize = 16;
    private const int ChallengeSize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const byte FormatVersion = 1;
    private const byte ModeDpapiOnly = 0;
    private const byte ModePassphrase = 1;
    private const byte ModeHello = 2;

    private static readonly byte[] Magic = "CVK1"u8.ToArray();

    // Fixed extra DPAPI entropy (not a secret); namespaces this app's blobs.
    private static readonly byte[] Entropy = "ClipVault.MasterKey.v1"u8.ToArray();
    private static readonly byte[] HelloInfo = "ClipVault.hello.v1"u8.ToArray();

    private readonly Argon2Parameters _argon = argon ?? Argon2Parameters.Secure;

    /// <summary>
    /// Determines whether the key file already exists.
    /// </summary>
    /// <returns><see langword="true"/> if the key file exists; otherwise, <see langword="false"/>.</returns>
    public bool Exists() => File.Exists(keyFilePath);

    /// <summary>
    /// Determines whether the key file is protected with a passphrase.
    /// </summary>
    /// <returns><see langword="true"/> if the key file requires a passphrase; otherwise, <see langword="false"/>.</returns>
    public bool RequiresPassphrase() => ReadMode() == ModePassphrase;

    /// <summary>
    /// Determines whether the key file is protected with Windows Hello.
    /// </summary>
    /// <returns><see langword="true"/> if the key file requires Windows Hello; otherwise, <see langword="false"/>.</returns>
    public bool RequiresHello() => ReadMode() == ModeHello;

    // --- DPAPI / passphrase (synchronous) ---

    /// <summary>
    /// Generates a fresh DEK, writes it to disk, and returns it.
    /// </summary>
    /// <param name="passphrase">The passphrase to protect the DEK with, or <see langword="null"/> for DPAPI-only protection.</param>
    /// <returns>The newly generated DEK.</returns>
    public byte[] CreateNew(string? passphrase)
    {
        var dek = RandomNumberGenerator.GetBytes(DekSize);
        Write(dek, passphrase);
        return dek;
    }

    /// <summary>
    /// Writes the DEK with DPAPI-only protection (when <paramref name="passphrase"/> is <see langword="null"/>)
    /// or with passphrase protection.
    /// </summary>
    /// <param name="dek">The DEK to write.</param>
    /// <param name="passphrase">The passphrase to protect the DEK with, or <see langword="null"/> for DPAPI-only protection.</param>
    public void Write(byte[] dek, string? passphrase)
    {
        var mode = passphrase is null ? ModeDpapiOnly : ModePassphrase;
        var body = passphrase is null ? dek : WrapWithPassphrase(dek, passphrase);
        WriteFramed(mode, body, zeroBody: passphrase is not null);
    }

    /// <summary>
    /// Reads and unprotects the DEK, decrypting it with the supplied passphrase when required.
    /// </summary>
    /// <param name="passphrase">The passphrase needed for a passphrase-protected key file, or <see langword="null"/> for a DPAPI-only key file.</param>
    /// <returns>The decrypted DEK.</returns>
    public byte[] Unlock(string? passphrase)
    {
        var (mode, body) = ReadAndUnprotect();
        return mode switch
        {
            ModeDpapiOnly => body,
            ModePassphrase when passphrase is not null => UnwrapWithPassphrase(body, passphrase),
            ModePassphrase => throw new CryptographicException("This vault requires a passphrase."),
            ModeHello => throw new CryptographicException("This vault requires Windows Hello."),
            _ => throw new CryptographicException("Unknown key protection mode."),
        };
    }

    /// <summary>
    /// Unlocks the DEK with the current passphrase and rewrites it under the new passphrase.
    /// </summary>
    /// <param name="currentPassphrase">The current passphrase, or <see langword="null"/> when the key file is currently DPAPI-only.</param>
    /// <param name="newPassphrase">The new passphrase, or <see langword="null"/> to switch to DPAPI-only protection.</param>
    public void ChangePassphrase(string? currentPassphrase, string? newPassphrase)
    {
        var dek = Unlock(currentPassphrase);
        try
        {
            Write(dek, newPassphrase);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    // --- Windows Hello (asynchronous) ---

    /// <summary>
    /// Generates a fresh DEK, writes it under Windows Hello protection, and returns it.
    /// </summary>
    /// <param name="hello">The Windows Hello service used to sign the challenge.</param>
    /// <returns>A task whose result is the newly generated DEK.</returns>
    public async Task<byte[]> CreateNewWithHelloAsync(IWindowsHello hello)
    {
        var dek = RandomNumberGenerator.GetBytes(DekSize);
        await WriteHelloAsync(dek, hello);
        return dek;
    }

    /// <summary>
    /// Writes a known DEK under Windows Hello protection, creating the credential if it is missing.
    /// </summary>
    /// <param name="dek">The DEK to write.</param>
    /// <param name="hello">The Windows Hello service used to sign the challenge.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    public async Task WriteHelloAsync(byte[] dek, IWindowsHello hello)
    {
        var challenge = RandomNumberGenerator.GetBytes(ChallengeSize);
        var signature = await hello.SignChallengeAsync(challenge, createIfMissing: true)
            ?? throw new CryptographicException("Windows Hello enrollment or signing failed.");
        var kek = DeriveHelloKek(signature);
        try
        {
            var (nonce, tag, wrapped) = Wrap(kek, dek);
            byte[] body = [.. challenge, .. nonce, .. tag, .. wrapped];
            WriteFramed(ModeHello, body, zeroBody: false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    /// <summary>
    /// Reads and unprotects a Windows Hello protected DEK, prompting the user to authenticate.
    /// </summary>
    /// <param name="hello">The Windows Hello service used to sign the stored challenge.</param>
    /// <returns>A task whose result is the decrypted DEK.</returns>
    public async Task<byte[]> UnlockWithHelloAsync(IWindowsHello hello)
    {
        var (mode, body) = ReadAndUnprotect();
        if (mode != ModeHello)
        {
            throw new CryptographicException("This vault is not protected with Windows Hello.");
        }

        var offset = 0;
        var challenge = body.AsSpan(offset, ChallengeSize).ToArray();
        offset += ChallengeSize;
        var nonce = body.AsSpan(offset, NonceSize).ToArray();
        offset += NonceSize;
        var tag = body.AsSpan(offset, TagSize).ToArray();
        offset += TagSize;
        var wrapped = body.AsSpan(offset, DekSize).ToArray();

        var signature = await hello.SignChallengeAsync(challenge, createIfMissing: false)
            ?? throw new CryptographicException("Windows Hello authentication failed.");
        var kek = DeriveHelloKek(signature);
        try
        {
            return Unwrap(kek, nonce, tag, wrapped);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    /// <summary>
    /// Crypto-erases the vault by deleting the key file, rendering all ciphertext unrecoverable.
    /// </summary>
    public void CryptoErase()
    {
        if (File.Exists(keyFilePath))
        {
            File.Delete(keyFilePath);
        }
    }

    // --- Internal helpers ---
    private static byte[] UnwrapWithPassphrase(byte[] body, string passphrase)
    {
        var offset = 0;
        var salt = body.AsSpan(offset, SaltSize).ToArray();
        offset += SaltSize;
        var memKiB = BitConverter.ToInt32(body, offset);
        offset += sizeof(int);
        var iterations = BitConverter.ToInt32(body, offset);
        offset += sizeof(int);
        var parallelism = BitConverter.ToInt32(body, offset);
        offset += sizeof(int);
        var nonce = body.AsSpan(offset, NonceSize).ToArray();
        offset += NonceSize;
        var tag = body.AsSpan(offset, TagSize).ToArray();
        offset += TagSize;
        var wrapped = body.AsSpan(offset, DekSize).ToArray();

        var kek = DeriveArgonKek(passphrase, salt, new Argon2Parameters(memKiB, iterations, parallelism));
        try
        {
            return Unwrap(kek, nonce, tag, wrapped);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    private static (byte[] Nonce, byte[] Tag, byte[] Wrapped) Wrap(byte[] kek, byte[] dek)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var wrapped = new byte[dek.Length];
        var tag = new byte[TagSize];
        using var aead = new ChaCha20Poly1305(kek);
        aead.Encrypt(nonce, dek, wrapped, tag);
        return (nonce, tag, wrapped);
    }

    private static byte[] Unwrap(byte[] kek, byte[] nonce, byte[] tag, byte[] wrapped)
    {
        var dek = new byte[wrapped.Length];
        using var aead = new ChaCha20Poly1305(kek);
        aead.Decrypt(nonce, wrapped, tag, dek); // Authentication failure (wrong passphrase / different person's Hello) -> AuthenticationTagMismatchException.
        return dek;
    }

    private static byte[] DeriveHelloKek(byte[] signature) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, signature, outputLength: 32, salt: null, info: HelloInfo);

    private static byte[] DeriveArgonKek(string passphrase, byte[] salt, Argon2Parameters p)
    {
        // Pin the passphrase bytes (so GC cannot relocate and leave a copy), then zero after Argon2.
        // The source string is immutable and cannot itself be zeroed (see PassphraseProvider).
        var pwBytes = new byte[Encoding.UTF8.GetByteCount(passphrase)];
        var handle = GCHandle.Alloc(pwBytes, GCHandleType.Pinned);
        try
        {
            Encoding.UTF8.GetBytes(passphrase, pwBytes);
            using var argon2 = new Argon2id(pwBytes)
            {
                Salt = salt,
                MemorySize = p.MemoryKiB,
                Iterations = p.Iterations,
                DegreeOfParallelism = p.Parallelism,
            };
            return argon2.GetBytes(32);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pwBytes);
            handle.Free();
        }
    }

    private byte ReadMode()
    {
        using var stream = File.OpenRead(keyFilePath);
        Span<byte> header = stackalloc byte[Magic.Length + 2];
        stream.ReadExactly(header);
        return header[Magic.Length + 1];
    }

    private (byte Mode, byte[] Body) ReadAndUnprotect()
    {
        var bytes = File.ReadAllBytes(keyFilePath);
        if (bytes.Length < Magic.Length + 2 || !bytes.AsSpan(0, Magic.Length).SequenceEqual(Magic))
        {
            throw new CryptographicException("The key file format is invalid.");
        }

        var mode = bytes[Magic.Length + 1];
        var body = ProtectedData.Unprotect(
            bytes.AsSpan(Magic.Length + 2).ToArray(), Entropy, DataProtectionScope.CurrentUser);
        return (mode, body);
    }

    private void WriteFramed(byte mode, byte[] body, bool zeroBody)
    {
        var protectedBody = ProtectedData.Protect(body, Entropy, DataProtectionScope.CurrentUser);
        if (zeroBody)
        {
            CryptographicOperations.ZeroMemory(body);
        }

        using var output = new MemoryStream();
        output.Write(Magic);
        output.WriteByte(FormatVersion);
        output.WriteByte(mode);
        output.Write(protectedBody);

        Directory.CreateDirectory(Path.GetDirectoryName(keyFilePath)!);
        File.WriteAllBytes(keyFilePath, output.ToArray());
    }

    private byte[] WrapWithPassphrase(byte[] dek, string passphrase)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var kek = DeriveArgonKek(passphrase, salt, _argon);
        try
        {
            var (nonce, tag, wrapped) = Wrap(kek, dek);
            using var ms = new MemoryStream();
            ms.Write(salt);
            ms.Write(BitConverter.GetBytes(_argon.MemoryKiB));
            ms.Write(BitConverter.GetBytes(_argon.Iterations));
            ms.Write(BitConverter.GetBytes(_argon.Parallelism));
            ms.Write(nonce);
            ms.Write(tag);
            ms.Write(wrapped);
            return ms.ToArray();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }
}
