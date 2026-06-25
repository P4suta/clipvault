namespace ClipVault.Application.Abstractions;

/// <summary>The UI theme. Persisted by name; applied immediately (no restart).</summary>
public enum AppTheme
{
    /// <summary>Follow the OS theme (the default).</summary>
    System,

    /// <summary>Always light.</summary>
    Light,

    /// <summary>Always dark.</summary>
    Dark,
}
