using System.ComponentModel;
using System.Diagnostics;
using ClipVault.Domain.ValueObjects;

namespace ClipVault.Infrastructure.Clipboard;

/// <summary>
/// Infers the source application of a clipboard change from the foreground window. For privacy, the window
/// title is not stored (only the process name and the executable path).
/// </summary>
public static class SourceAppResolver
{
    /// <summary>
    /// Resolves the source application that owns the current foreground window.
    /// </summary>
    /// <returns>
    /// The resolved source application, or <see cref="SourceApplication.Unknown"/> when it cannot be determined.
    /// </returns>
    public static SourceApplication Resolve()
    {
        try
        {
            var hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == 0)
            {
                return SourceApplication.Unknown;
            }

            _ = NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0)
            {
                return SourceApplication.Unknown;
            }

            using var process = Process.GetProcessById((int)pid);
            string? executablePath = null;
            try
            {
                executablePath = process.MainModule?.FileName;
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
            {
                // MainModule is not accessible for processes of a different architecture or elevated processes. Continue with the process name only.
            }

            return new SourceApplication(process.ProcessName, WindowTitle: null, executablePath);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // The target process may have exited by the time it is queried, and so on. Continue treating the source as unknown.
            return SourceApplication.Unknown;
        }
    }
}
