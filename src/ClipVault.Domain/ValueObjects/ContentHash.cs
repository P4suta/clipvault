namespace ClipVault.Domain.ValueObjects;

/// <summary>
/// A hash of the content used for duplicate detection. Rather than a naive hash of the plaintext,
/// a keyed HMAC is expected (so the content cannot be inferred from the stored hash). The
/// computation is performed by <c>IEncryptionService</c>.
/// </summary>
public readonly record struct ContentHash
{
    /// <summary>Initializes a new instance of the <see cref="ContentHash"/> struct.</summary>
    /// <param name="value">The hash value. Must not be null or white space.</param>
    public ContentHash(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the underlying hash value.</summary>
    public string Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
