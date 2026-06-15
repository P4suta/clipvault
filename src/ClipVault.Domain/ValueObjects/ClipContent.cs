using System;
using System.Security.Cryptography;

namespace ClipVault.Domain.ValueObjects;

/// <summary>
/// The decrypted clipboard content itself (text as UTF-8 bytes, images as PNG bytes).
/// This is the in-memory raw data fetched when pasting back or showing a full-size preview.
/// </summary>
/// <param name="Type">The kind of clipboard content.</param>
/// <param name="Payload">The raw content bytes.</param>
public sealed record ClipContent(ClipContentType Type, byte[] Payload) : IDisposable
{
    /// <summary>Gets the size of the content in bytes.</summary>
    public long SizeInBytes => Payload.LongLength;

    /// <summary>
    /// Zeroes the decrypted payload. Callers dispose a materialized instance once done; <c>MaterializeAsync</c>
    /// returns a caller-owned copy. Best-effort: copies handed to WinRT and UI strings cannot be zeroed.
    /// </summary>
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(Payload);
        GC.SuppressFinalize(this);
    }
}
