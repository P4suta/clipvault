using System.Security;
using System.Text.Json;
using ClipVault.Application.Abstractions;

namespace ClipVault.Infrastructure.Settings;

/// <summary>
/// Persists settings to %LOCALAPPDATA%\ClipVault\settings.json. It stores only user settings (storage mode,
/// excluded apps, retention limits, and so on), not clipboard content. Reading and writing go through a DTO
/// for robustness.
/// </summary>
public sealed class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private ClipVaultSettings _current;

    private JsonSettingsService(string path, ClipVaultSettings initial)
    {
        _path = path;
        _current = initial;
    }

    /// <inheritdoc/>
    public event EventHandler? Changed;

    /// <inheritdoc/>
    public ClipVaultSettings Current => _current;

    /// <summary>
    /// Gets the default settings file path under %LOCALAPPDATA%\ClipVault.
    /// </summary>
    /// <returns>The default settings file path.</returns>
    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipVault", "settings.json");

    /// <summary>
    /// Loads the settings from a file, falling back to the defaults when the file is missing or corrupt.
    /// </summary>
    /// <param name="path">The path to the settings file.</param>
    /// <returns>A settings service initialized from the file or with the default settings.</returns>
    public static JsonSettingsService Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var dto = JsonSerializer.Deserialize<SettingsDto>(File.ReadAllText(path));
                if (dto is not null)
                {
                    return new JsonSettingsService(path, dto.ToSettings());
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException
            or JsonException or NotSupportedException or ArgumentException)
        {
            // Corrupt or unreadable settings fall back to the defaults (do not block startup).
        }

        return new JsonSettingsService(path, ClipVaultSettings.Default);
    }

    /// <inheritdoc/>
    public void Update(ClipVaultSettings settings)
    {
        _current = settings;
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(SettingsDto.FromSettings(_current), JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException
            or NotSupportedException)
        {
            // Best effort (keep the app running even when the disk is unavailable).
        }
    }

    // The DTO used for persistence (avoids interface types and stores enums as strings for stable persistence).
    private sealed record SettingsDto
    {
        public string Storage { get; init; } = nameof(StorageMode.VolatileMemory);

        public string[] ExcludedProcessNames { get; init; } = [];

        public bool MaskGenericPasswords { get; init; }

        public bool StripTrackingParameters { get; init; }

        public long MaxImageBytes { get; init; } = 10L * 1024 * 1024;

        public int MaxAgeDays { get; init; } = 30;

        public int MaxEntries { get; init; } = 500;

        public bool RunAtStartup { get; init; }

        public string Language { get; init; } = nameof(AppLanguage.System);

        public string Theme { get; init; } = nameof(AppTheme.System);

        public ClipVaultSettings ToSettings() => new()
        {
            Storage = Enum.TryParse<StorageMode>(Storage, out var mode) ? mode : StorageMode.EncryptedDisk,
            ExcludedProcessNames = new HashSet<string>(ExcludedProcessNames, StringComparer.OrdinalIgnoreCase),
            MaskGenericPasswords = MaskGenericPasswords,
            StripTrackingParameters = StripTrackingParameters,
            MaxImageBytes = MaxImageBytes,
            MaxAgeDays = MaxAgeDays,
            MaxEntries = MaxEntries,
            RunAtStartup = RunAtStartup,
            Language = Enum.TryParse<AppLanguage>(Language, out var language) ? language : AppLanguage.System,
            Theme = Enum.TryParse<AppTheme>(Theme, out var theme) ? theme : AppTheme.System,
        };

        public static SettingsDto FromSettings(ClipVaultSettings s) => new()
        {
            Storage = s.Storage.ToString(),
            ExcludedProcessNames = [.. s.ExcludedProcessNames],
            MaskGenericPasswords = s.MaskGenericPasswords,
            StripTrackingParameters = s.StripTrackingParameters,
            MaxImageBytes = s.MaxImageBytes,
            MaxAgeDays = s.MaxAgeDays,
            MaxEntries = s.MaxEntries,
            RunAtStartup = s.RunAtStartup,
            Language = s.Language.ToString(),
            Theme = s.Theme.ToString(),
        };
    }
}
