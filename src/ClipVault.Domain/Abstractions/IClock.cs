namespace ClipVault.Domain.Abstractions;

/// <summary>An abstraction over the current time. <c>DateTimeOffset.UtcNow</c> is not used directly so that tests can control it deterministically.</summary>
public interface IClock
{
    /// <summary>Gets the current Coordinated Universal Time (UTC).</summary>
    DateTimeOffset UtcNow { get; }
}
