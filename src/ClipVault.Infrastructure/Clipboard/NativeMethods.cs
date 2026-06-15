using System.Runtime.InteropServices;

namespace ClipVault.Infrastructure.Clipboard;

/// <summary>
/// The minimal set of Win32 P/Invoke declarations (trim-safe via source generation).
/// </summary>
internal static partial class NativeMethods
{
    /// <summary>
    /// Retrieves a handle to the foreground window.
    /// </summary>
    /// <returns>A handle to the foreground window, or zero when there is none.</returns>
    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial nint GetForegroundWindow();

    /// <summary>
    /// Retrieves the identifier of the process that created the given window.
    /// </summary>
    /// <param name="hWnd">A handle to the window.</param>
    /// <param name="lpdwProcessId">When this method returns, contains the process identifier.</param>
    /// <returns>The identifier of the thread that created the window.</returns>
    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);
}
