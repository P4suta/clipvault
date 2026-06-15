namespace ClipVault.Domain.ValueObjects;

/// <summary>A unique identifier for a history entry.</summary>
/// <param name="Value">The underlying GUID value of the identifier.</param>
public readonly record struct EntryId(Guid Value)
{
    /// <summary>Creates a new identifier.</summary>
    /// <returns>A new <see cref="EntryId"/> with a freshly generated value.</returns>
    public static EntryId New() => new(Guid.NewGuid());

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();
}
