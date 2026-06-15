using ClipVault.Application.Abstractions;
using Microsoft.Win32;

namespace ClipVault.Infrastructure.Startup;

/// <summary>
/// Run-at-logon startup for the unpackaged build. Registers or unregisters the current executable under
/// HKCU\...\Run. (The MSIX StartupTask is unavailable when unpackaged, so the registry is used instead.)
/// </summary>
public sealed class RegistryStartupService : IStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ClipVault";

    /// <inheritdoc/>
    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) is not null;
    }

    /// <inheritdoc/>
    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (enabled)
        {
            var executablePath = Environment.ProcessPath;
            if (executablePath is not null)
            {
                key.SetValue(ValueName, $"\"{executablePath}\"");
            }
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
