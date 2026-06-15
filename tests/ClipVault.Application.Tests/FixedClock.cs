using ClipVault.Domain.Abstractions;

namespace ClipVault.Application.Tests;

/// <summary>A fake clock that returns a fixed time.</summary>
internal sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}
