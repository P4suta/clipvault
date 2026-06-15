using ClipVault.Domain.Abstractions;

namespace ClipVault.Infrastructure.Time;

/// <summary>
/// The system clock.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc/>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
