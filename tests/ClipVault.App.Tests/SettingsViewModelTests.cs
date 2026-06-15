using ClipVault.Application.Abstractions;
using ClipVault.Application.Settings;
using ClipVaultApp.Localization;
using ClipVaultApp.ViewModels;
using NSubstitute;

namespace ClipVault.App.Tests;

public class SettingsViewModelTests
{
    // ---------- Storage mode ----------
    [Fact]
    public void Loads_storage_mode_from_settings()
    {
        var vm = Build(DiskSettings());

        Assert.True(vm.IsEncryptedDisk);
        Assert.False(vm.IsVolatileMemory);
    }

    [Fact]
    public void Selecting_encrypted_disk_clears_volatile_and_persists()
    {
        var settings = new InMemorySettingsService(); // Default is volatile.
        var vm = Build(settings);
        Assert.True(vm.IsVolatileMemory);

        vm.IsEncryptedDisk = true;

        Assert.False(vm.IsVolatileMemory);
        Assert.Equal(StorageMode.EncryptedDisk, settings.Current.Storage);
    }

    [Theory]
    [InlineData(StorageMode.EncryptedDisk, "暗号化ディスク（永続）")]
    [InlineData(StorageMode.VolatileMemory, "メモリ揮発（ディスクに残さない）")]
    public void Storage_display_reflects_the_setting(StorageMode mode, string expected)
    {
        var settings = new InMemorySettingsService();
        settings.Update(ClipVaultSettings.Default with { Storage = mode });

        Assert.Equal(expected, Build(settings).StorageDisplay);
    }

    // ---------- Protection ----------
    [Theory]
    [InlineData(VaultProtection.DpapiOnly, "DPAPI のみ（パスフレーズ／Hello 未設定）")]
    [InlineData(VaultProtection.Passphrase, "DPAPI ＋ パスフレーズ（二要素）")]
    [InlineData(VaultProtection.Hello, "DPAPI ＋ Windows Hello（二要素）")]
    [InlineData(VaultProtection.Volatile, "メモリ揮発（鍵はディスクに無い）")]
    public void Protection_display_reflects_vault_state(VaultProtection protection, string expected)
    {
        var vault = Substitute.For<IVaultManagement>();
        vault.Protection.Returns(protection);

        Assert.Equal(expected, Build(new InMemorySettingsService(), vault).ProtectionDisplay);
    }

    [Fact]
    public void Hello_protection_hides_the_passphrase_controls()
    {
        var vault = Substitute.For<IVaultManagement>();
        vault.Protection.Returns(VaultProtection.Hello);

        var vm = Build(new InMemorySettingsService(), vault);

        Assert.True(vm.IsHelloProtected);
        Assert.True(vm.IsPassphraseSectionVisible);
        Assert.False(vm.IsPassphraseControlsVisible);
    }

    [Fact]
    public void Passphrase_protection_marks_has_passphrase()
    {
        var vault = Substitute.For<IVaultManagement>();
        vault.Protection.Returns(VaultProtection.Passphrase);

        var vm = Build(new InMemorySettingsService(), vault);

        Assert.True(vm.HasPassphrase);
        Assert.True(vm.IsPassphraseControlsVisible);
    }

    [Fact]
    public void Volatile_protection_hides_the_passphrase_section()
    {
        var vault = Substitute.For<IVaultManagement>();
        vault.Protection.Returns(VaultProtection.Volatile);

        Assert.False(Build(new InMemorySettingsService(), vault).IsPassphraseSectionVisible);
    }

    // ---------- Excluded apps ----------
    [Fact]
    public void Add_excluded_app_strips_exe_and_persists()
    {
        var settings = WithExclusions();
        var vm = Build(settings);
        vm.NewExcludedApp = "Notepad.EXE";

        vm.AddExcludedAppCommand.Execute(null);

        Assert.Equal("Notepad", Assert.Single(vm.ExcludedApps).Name);
        Assert.Equal(string.Empty, vm.NewExcludedApp);
        Assert.Contains("Notepad", settings.Current.ExcludedProcessNames);
    }

    [Fact]
    public void Add_excluded_app_ignores_case_insensitive_duplicates()
    {
        var vm = Build(WithExclusions("foo"));
        vm.NewExcludedApp = "FOO";

        vm.AddExcludedAppCommand.Execute(null);

        Assert.Single(vm.ExcludedApps);
    }

    [Fact]
    public void Excluded_apps_are_kept_sorted()
    {
        var vm = Build(WithExclusions());
        var inputs = new[] { "zebra", "alpha", "mango" };
        foreach (var name in inputs)
        {
            vm.NewExcludedApp = name;
            vm.AddExcludedAppCommand.Execute(null);
        }

        var expected = new[] { "alpha", "mango", "zebra" };
        Assert.Equal(expected, vm.ExcludedApps.Select(a => a.Name));
    }

    [Fact]
    public void Remove_excluded_app_persists()
    {
        var settings = WithExclusions("foo", "bar");
        var vm = Build(settings);
        var target = vm.ExcludedApps.First(a => string.Equals(a.Name, "bar", StringComparison.Ordinal));

        vm.RemoveExcludedAppCommand.Execute(target);

        Assert.DoesNotContain(vm.ExcludedApps, a => string.Equals(a.Name, "bar", StringComparison.Ordinal));
        Assert.DoesNotContain("bar", settings.Current.ExcludedProcessNames);
    }

    // ---------- Passphrase validation ----------
    [Fact]
    public async Task Save_passphrase_requires_a_new_passphrase()
    {
        var vault = Substitute.For<IVaultManagement>();
        var vm = Build(DiskSettings(), vault);
        vm.NewPassphrase = string.Empty;

        await vm.SavePassphraseCommand.ExecuteAsync(null);

        Assert.True(vm.HasPassphraseError);
        await vault.DidNotReceive()
            .SetOrChangePassphraseAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_passphrase_requires_matching_confirmation()
    {
        var vault = Substitute.For<IVaultManagement>();
        var vm = Build(DiskSettings(), vault);
        vm.NewPassphrase = "abc";
        vm.ConfirmPassphrase = "xyz";

        await vm.SavePassphraseCommand.ExecuteAsync(null);

        Assert.True(vm.HasPassphraseError);
        await vault.DidNotReceive()
            .SetOrChangePassphraseAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ---------- Language ----------
    [Fact]
    public void Loads_selected_language_from_settings()
    {
        var settings = new InMemorySettingsService();
        settings.Update(ClipVaultSettings.Default with { Language = AppLanguage.English });

        var vm = Build(settings);

        Assert.NotNull(vm.SelectedLanguage);
        Assert.Equal(AppLanguage.English, vm.SelectedLanguage!.Value);
    }

    [Fact]
    public void Defaults_to_system_language_when_unset()
    {
        Assert.Equal(AppLanguage.System, Build(new InMemorySettingsService()).SelectedLanguage!.Value);
    }

    [Fact]
    public void Selecting_a_language_persists_it()
    {
        var settings = new InMemorySettingsService();
        var vm = Build(settings);

        vm.SelectedLanguage = vm.LanguageOptions.First(o => o.Value == AppLanguage.ChineseSimplified);

        Assert.Equal(AppLanguage.ChineseSimplified, settings.Current.Language);
    }

    // ---------- Retention ----------
    [Fact]
    public void Retention_age_is_clamped_to_at_least_one()
    {
        var vm = Build(new InMemorySettingsService());

        vm.MaxAgeDays = 0;

        Assert.Equal(1, vm.MaxAgeDays);
    }

    [Fact]
    public void Retention_count_is_clamped_to_at_least_one()
    {
        var vm = Build(new InMemorySettingsService());

        vm.MaxEntries = -5;

        Assert.Equal(1, vm.MaxEntries);
    }

    [Fact]
    public void Retention_double_proxy_rounds_and_ignores_nan()
    {
        var vm = Build(new InMemorySettingsService());

        vm.MaxAgeDaysValue = 12.6;
        Assert.Equal(13, vm.MaxAgeDays);

        vm.MaxAgeDaysValue = double.NaN;
        Assert.Equal(13, vm.MaxAgeDays);
    }

    private static SettingsViewModel Build(
        ISettingsService settings, IVaultManagement? vault = null, IStartupService? startup = null) =>
        new(
            settings,
            vault ?? Substitute.For<IVaultManagement>(),
            startup ?? Substitute.For<IStartupService>(),
            new LocalizationService(AppLanguage.Japanese));

    private static InMemorySettingsService DiskSettings()
    {
        var settings = new InMemorySettingsService();
        settings.Update(ClipVaultSettings.Default with { Storage = StorageMode.EncryptedDisk });
        return settings;
    }

    private static InMemorySettingsService WithExclusions(params string[] names)
    {
        var settings = new InMemorySettingsService();
        settings.Update(ClipVaultSettings.Default with
        {
            ExcludedProcessNames = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase),
        });
        return settings;
    }
}
