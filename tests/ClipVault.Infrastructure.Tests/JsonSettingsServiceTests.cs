using ClipVault.Application.Abstractions;
using ClipVault.Infrastructure.Settings;

namespace ClipVault.Infrastructure.Tests;

public sealed class JsonSettingsServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public JsonSettingsServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ClipVaultSettings_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "settings.json");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // Best effort.
        }
    }

    [Fact]
    public void Uses_defaults_when_file_missing()
    {
        var service = JsonSettingsService.Load(_path);

        Assert.Equal(StorageMode.VolatileMemory, service.Current.Storage);
        Assert.NotEmpty(service.Current.ExcludedProcessNames);
    }

    [Fact]
    public void Update_persists_and_reloads_round_trip()
    {
        var service = JsonSettingsService.Load(_path);
        service.Update(ClipVaultSettings.Default with
        {
            Storage = StorageMode.VolatileMemory,
            MaskGenericPasswords = true,
            MaxAgeDays = 7,
            MaxEntries = 42,
            RunAtStartup = true,
            ExcludedProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "customapp" },
            Language = AppLanguage.English,
        });

        var reloaded = JsonSettingsService.Load(_path).Current;

        Assert.Equal(StorageMode.VolatileMemory, reloaded.Storage);
        Assert.True(reloaded.MaskGenericPasswords);
        Assert.Equal(7, reloaded.MaxAgeDays);
        Assert.Equal(42, reloaded.MaxEntries);
        Assert.True(reloaded.RunAtStartup);
        Assert.Contains("CUSTOMAPP", reloaded.ExcludedProcessNames); // Restored case-insensitively.
        Assert.Equal(AppLanguage.English, reloaded.Language);
    }

    [Fact]
    public void Default_language_is_system_when_file_missing() => Assert.Equal(AppLanguage.System, JsonSettingsService.Load(_path).Current.Language);

    [Fact]
    public void Unknown_language_falls_back_to_system()
    {
        File.WriteAllText(_path, "{\"Language\":\"klingon\"}");

        Assert.Equal(AppLanguage.System, JsonSettingsService.Load(_path).Current.Language);
    }

    [Fact]
    public void Missing_language_field_falls_back_to_system()
    {
        File.WriteAllText(_path, "{\"Storage\":\"EncryptedDisk\"}");

        var reloaded = JsonSettingsService.Load(_path).Current;

        Assert.Equal(AppLanguage.System, reloaded.Language);
        Assert.Equal(StorageMode.EncryptedDisk, reloaded.Storage);
    }

    [Fact]
    public void Theme_round_trips()
    {
        var service = JsonSettingsService.Load(_path);
        service.Update(ClipVaultSettings.Default with { Theme = AppTheme.Dark });

        Assert.Equal(AppTheme.Dark, JsonSettingsService.Load(_path).Current.Theme);
    }

    [Fact]
    public void Unknown_theme_falls_back_to_system()
    {
        File.WriteAllText(_path, "{\"Theme\":\"plaid\"}");

        Assert.Equal(AppTheme.System, JsonSettingsService.Load(_path).Current.Theme);
    }

    [Fact]
    public void Truncated_json_falls_back_to_defaults()
    {
        File.WriteAllText(_path, "{\"Storage\":");

        Assert.Equal(StorageMode.VolatileMemory, JsonSettingsService.Load(_path).Current.Storage);
    }

    [Fact]
    public void A_wrong_typed_field_falls_back_to_defaults()
    {
        File.WriteAllText(_path, "{\"MaxAgeDays\":\"seven\"}");

        Assert.Equal(30, JsonSettingsService.Load(_path).Current.MaxAgeDays);
    }
}
