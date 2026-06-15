using ClipVault.Application.Abstractions;
using Windows.Security.Credentials;
using Windows.Security.Cryptography;

namespace ClipVault.Infrastructure.Security;

/// <summary>
/// Signing with Windows Hello (<see cref="KeyCredentialManager"/>). The credential is protected by the TPM, and
/// biometrics or a PIN are requested when a signature is requested. A signature over a fixed challenge is
/// deterministic, so it can be used as key material.
/// </summary>
public sealed class WindowsHelloService : IWindowsHello
{
    private const string CredentialName = "ClipVault";

    /// <inheritdoc/>
    public async Task<bool> IsAvailableAsync() => await KeyCredentialManager.IsSupportedAsync();

    /// <inheritdoc/>
    public async Task<byte[]?> SignChallengeAsync(byte[] challenge, bool createIfMissing)
    {
        var credential = await GetCredentialAsync(createIfMissing);
        if (credential is null)
        {
            return null;
        }

        var result = await credential.RequestSignAsync(CryptographicBuffer.CreateFromByteArray(challenge));
        if (result.Status != KeyCredentialStatus.Success)
        {
            return null;
        }

        CryptographicBuffer.CopyToByteArray(result.Result, out var signature);
        return signature;
    }

    private static async Task<KeyCredential?> GetCredentialAsync(bool createIfMissing)
    {
        var open = await KeyCredentialManager.OpenAsync(CredentialName);
        if (open.Status == KeyCredentialStatus.Success)
        {
            return open.Credential;
        }

        if (!createIfMissing)
        {
            return null;
        }

        var created = await KeyCredentialManager.RequestCreateAsync(
            CredentialName, KeyCredentialCreationOption.ReplaceExisting);
        return created.Status == KeyCredentialStatus.Success ? created.Credential : null;
    }
}
