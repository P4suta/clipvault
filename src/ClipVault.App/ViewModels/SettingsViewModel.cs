using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using ClipVault.Application.Abstractions;
using ClipVaultApp.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClipVaultApp.ViewModels;

/// <summary>
/// ViewModel for the settings window. It bridges storage mode, passphrase protection, panic wipe,
/// excluded apps, masking, retention limits, and auto-start to the back end
/// (<see cref="ISettingsService"/> / <see cref="IVaultManagement"/> / <see cref="IStartupService"/>).
/// The panic-wipe confirmation dialog and the actual exit require a XamlRoot, so they are handled by
/// the window; the ViewModel requests them via an event (separation of concerns).
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IVaultManagement _vault;
    private readonly IStartupService _startup;
    private readonly ILocalizationService _loc;

    /// <summary>Guard to prevent a feedback loop between UI changes and service reflection.</summary>
    private bool _isSyncing;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsViewModel"/> class.
    /// </summary>
    /// <param name="settings">The settings service holding the current configuration.</param>
    /// <param name="vault">The vault management service for protection operations.</param>
    /// <param name="startup">The startup service controlling auto-start.</param>
    /// <param name="loc">The localization service used for display and message strings.</param>
    public SettingsViewModel(
        ISettingsService settings,
        IVaultManagement vault,
        IStartupService startup,
        ILocalizationService loc)
    {
        _settings = settings;
        _vault = vault;
        _startup = startup;
        _loc = loc;

        // Language picker: own-language endonyms for the concrete languages (so a user can always find
        // theirs), and a localized label for the OS-following default.
        LanguageOptions =
        [
            new LanguageOption(AppLanguage.System, _loc.GetString("Settings.Language.System")),
            new LanguageOption(AppLanguage.Japanese, "日本語"),
            new LanguageOption(AppLanguage.English, "English"),
            new LanguageOption(AppLanguage.ChineseSimplified, "简体中文"),
        ];

        LoadFromBackend();

        // Hello availability is queried asynchronously. Update the UI (enable button / hint) once known.
        _ = InitializeHelloAvailabilityAsync();
    }

    /// <summary>
    /// Occurs when a panic wipe is requested. The window handles confirmation dialog, execution, and exit.
    /// </summary>
    public event EventHandler? PanicWipeRequested;

    // --- Storage mode ---

    /// <summary>Gets or sets a value indicating whether storage is encrypted disk.</summary>
    [ObservableProperty]
    public partial bool IsEncryptedDisk { get; set; }

    /// <summary>Gets or sets a value indicating whether storage is volatile memory.</summary>
    [ObservableProperty]
    public partial bool IsVolatileMemory { get; set; }

    // --- Language ---

    /// <summary>Gets the selectable UI languages (fixed list).</summary>
    public IReadOnlyList<LanguageOption> LanguageOptions { get; }

    /// <summary>Gets or sets the selected UI language. The change is persisted and applied on the next restart.</summary>
    [ObservableProperty]
    public partial LanguageOption? SelectedLanguage { get; set; }

    // --- Passphrase protection ---

    /// <summary>Gets the display string for the current storage mode (for the privacy panel).</summary>
    [ObservableProperty]
    public partial string StorageDisplay { get; private set; } = string.Empty;

    /// <summary>Gets the display string for the current protection state (DPAPI only / passphrase two-factor / volatile memory).</summary>
    [ObservableProperty]
    public partial string ProtectionDisplay { get; private set; } = string.Empty;

    /// <summary>Gets the privacy-panel guarantee text scoped to the running mode (volatile / DPAPI / passphrase / Hello).</summary>
    [ObservableProperty]
    public partial string AtRestGuarantee { get; private set; } = string.Empty;

    /// <summary>Gets the privacy-panel "additional safeguards" text: the extra protection that is enabled, or can be enabled, for the running mode.</summary>
    [ObservableProperty]
    public partial string ExtraProtection { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether a storage-mode change is selected but not yet applied (it takes effect on the next restart).</summary>
    [ObservableProperty]
    public partial bool IsStorageRestartPending { get; private set; }

    /// <summary>Gets a value indicating whether the protection UI (passphrase / Hello) is shown; it is hidden entirely in volatile mode.</summary>
    [ObservableProperty]
    public partial bool IsPassphraseSectionVisible { get; private set; }

    /// <summary>Gets a value indicating whether passphrase protection is currently enabled (controls the "current passphrase" field / "remove" button).</summary>
    [ObservableProperty]
    public partial bool HasPassphrase { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the set/change/remove passphrase UI is shown. Shown for
    /// non-volatile modes other than Hello (DPAPI only / passphrase). While Hello is active the key
    /// is sealed by Hello, so a passphrase cannot be set directly (Hello must be disabled first).
    /// </summary>
    [ObservableProperty]
    public partial bool IsPassphraseControlsVisible { get; private set; }

    /// <summary>Gets a value indicating whether Windows Hello is available on this PC (if not, a hint is shown and the button is disabled).</summary>
    [ObservableProperty]
    public partial bool IsHelloAvailable { get; private set; }

    /// <summary>Gets a value indicating whether Windows Hello protection is currently enabled (controls the "disable Hello protection" button).</summary>
    [ObservableProperty]
    public partial bool IsHelloProtected { get; private set; }

    /// <summary>Gets a value indicating whether the "protect with Windows Hello" button is shown (non-volatile, available, and not yet using Hello).</summary>
    [ObservableProperty]
    public partial bool IsHelloEnableVisible { get; private set; }

    /// <summary>Gets a value indicating whether the "Windows Hello unavailable" hint is shown (non-volatile, unavailable, and not yet using Hello).</summary>
    [ObservableProperty]
    public partial bool IsHelloUnavailableHintVisible { get; private set; }

    /// <summary>Gets or sets the current passphrase entered by the user.</summary>
    [ObservableProperty]
    public partial string CurrentPassphrase { get; set; } = string.Empty;

    /// <summary>Gets or sets the new passphrase entered by the user.</summary>
    [ObservableProperty]
    public partial string NewPassphrase { get; set; } = string.Empty;

    /// <summary>Gets or sets the confirmation passphrase entered by the user.</summary>
    [ObservableProperty]
    public partial string ConfirmPassphrase { get; set; } = string.Empty;

    /// <summary>Gets a value indicating whether a passphrase operation is in progress (Argon2id running); used to disable buttons and show the ring.</summary>
    [ObservableProperty]
    public partial bool IsPassphraseBusy { get; private set; }

    /// <summary>Gets the current passphrase error message, if any.</summary>
    [ObservableProperty]
    public partial string PassphraseError { get; private set; } = string.Empty;

    /// <summary>Gets the current passphrase success message, if any.</summary>
    [ObservableProperty]
    public partial string PassphraseSuccess { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether there is a passphrase error message to show.</summary>
    public bool HasPassphraseError => !string.IsNullOrEmpty(PassphraseError);

    /// <summary>Gets a value indicating whether there is a passphrase success message to show.</summary>
    public bool HasPassphraseSuccess => !string.IsNullOrEmpty(PassphraseSuccess);

    // --- Excluded apps ---

    /// <summary>Gets the editable list of excluded process names (kept sorted to stabilize display order).</summary>
    public ObservableCollection<ExcludedAppViewModel> ExcludedApps { get; } = [];

    /// <summary>Gets or sets the process name to be added to the exclusion list.</summary>
    [ObservableProperty]
    public partial string NewExcludedApp { get; set; } = string.Empty;

    // --- Masking ---

    /// <summary>Gets or sets a value indicating whether generic passwords are masked.</summary>
    [ObservableProperty]
    public partial bool MaskGenericPasswords { get; set; }

    /// <summary>Gets or sets a value indicating whether tracking parameters are stripped from captured URLs.</summary>
    [ObservableProperty]
    public partial bool StripTrackingParameters { get; set; }

    // --- Retention ---
    // NumberBox.Value is a double, so it is exposed to the UI via double proxies and bridged to the settings (int).

    /// <summary>Gets or sets the maximum retention age in days.</summary>
    [ObservableProperty]
    public partial int MaxAgeDays { get; set; }

    /// <summary>Gets or sets the maximum number of retained entries.</summary>
    [ObservableProperty]
    public partial int MaxEntries { get; set; }

    /// <summary>Gets or sets the double proxy for NumberBox (retention days).</summary>
    public double MaxAgeDaysValue
    {
        get => MaxAgeDays;
        set => MaxAgeDays = double.IsNaN(value) ? MaxAgeDays : (int)Math.Round(value);
    }

    /// <summary>Gets or sets the double proxy for NumberBox (retention count).</summary>
    public double MaxEntriesValue
    {
        get => MaxEntries;
        set => MaxEntries = double.IsNaN(value) ? MaxEntries : (int)Math.Round(value);
    }

    // --- Auto-start ---

    /// <summary>Gets or sets a value indicating whether the app runs at startup.</summary>
    [ObservableProperty]
    public partial bool RunAtStartup { get; set; }

    /// <summary>Executes the actual wipe after the user has confirmed it on the window side.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task ExecutePanicWipeAsync() => _vault.PanicWipeAsync();

    private string DescribePassphraseFailure(Exception ex) => ex switch
    {
        System.Security.Cryptography.CryptographicException =>
            _loc.GetString("Settings.Msg.WrongCurrentPassphrase"),
        InvalidOperationException io => io.Message,
        _ => _loc.GetString("Settings.Msg.OperationFailed"),
    };

    private string DescribeHelloFailure(Exception ex) => ex switch
    {
        // When the passphrase is wrong during EnableHello, unlocking the current key throws a cryptographic exception.
        System.Security.Cryptography.AuthenticationTagMismatchException =>
            _loc.GetString("Settings.Msg.WrongCurrentPassphrase"),

        // Hello cancellation / authentication failure / no credential, or a wrong passphrase, etc.
        System.Security.Cryptography.CryptographicException =>
            _loc.GetString("Settings.Msg.HelloFailedOrCancelled"),
        InvalidOperationException io => io.Message,
        _ => _loc.GetString("Settings.Msg.OperationFailed"),
    };

    /// <summary>Queries Windows Hello availability and reflects it into the visibility flags.</summary>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Best-effort availability query; treat failure as unavailable.")]
    private async Task InitializeHelloAvailabilityAsync()
    {
        bool available;
        try
        {
            available = await _vault.IsHelloAvailableAsync();
        }
        catch
        {
            // If the query itself fails, treat it as "unavailable" (fail safe).
            available = false;
        }

        IsHelloAvailable = available;
        RefreshProtection();
    }

    /// <summary>Loads the current back-end values into the UI (with the two-way loop suppressed).</summary>
    private void LoadFromBackend()
    {
        _isSyncing = true;
        try
        {
            var current = _settings.Current;

            IsEncryptedDisk = current.Storage == StorageMode.EncryptedDisk;
            IsVolatileMemory = current.Storage == StorageMode.VolatileMemory;

            SelectedLanguage = LanguageOptions.FirstOrDefault(o => o.Value == current.Language)
                ?? LanguageOptions[0];

            MaskGenericPasswords = current.MaskGenericPasswords;
            StripTrackingParameters = current.StripTrackingParameters;
            MaxAgeDays = current.MaxAgeDays;
            MaxEntries = current.MaxEntries;

            ExcludedApps.Clear();
            foreach (var name in current.ExcludedProcessNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                ExcludedApps.Add(new ExcludedAppViewModel(name));
            }

            // Trust the actual source of truth (registry) for auto-start.
            RunAtStartup = _startup.IsEnabled();

            RefreshProtection();
        }
        finally
        {
            _isSyncing = false;
        }
    }

    /// <summary>
    /// Re-reads the vault protection state and refreshes the privacy/protection display. Everything here reflects the
    /// CURRENT running session: a storage-mode change only takes effect on restart, so the display follows the active
    /// protection (not the pending setting), and a pending change is surfaced via <see cref="IsStorageRestartPending"/>.
    /// </summary>
    private void RefreshProtection()
    {
        var protection = _vault.Protection;

        StorageDisplay = protection == VaultProtection.Volatile
            ? _loc.GetString("Settings.StorageDisplay.Volatile")
            : _loc.GetString("Settings.StorageDisplay.Encrypted");

        // The protection UI (passphrase / Hello) is hidden entirely in volatile mode.
        IsPassphraseSectionVisible = protection != VaultProtection.Volatile;

        HasPassphrase = protection == VaultProtection.Passphrase;
        IsHelloProtected = protection == VaultProtection.Hello;

        // The passphrase editing UI is shown only for DPAPI-only / passphrase (not while Hello is active).
        IsPassphraseControlsVisible =
            protection == VaultProtection.DpapiOnly || protection == VaultProtection.Passphrase;

        // "Protect with Hello" only when non-volatile, available, and not yet using Hello.
        IsHelloEnableVisible = IsPassphraseSectionVisible && IsHelloAvailable && !IsHelloProtected;

        // The unavailable hint only when non-volatile, unavailable, and not yet using Hello.
        IsHelloUnavailableHintVisible = IsPassphraseSectionVisible && !IsHelloAvailable && !IsHelloProtected;

        ProtectionDisplay = protection switch
        {
            VaultProtection.DpapiOnly => _loc.GetString("Settings.ProtectionDisplay.DpapiOnly"),
            VaultProtection.Passphrase => _loc.GetString("Settings.ProtectionDisplay.Passphrase"),
            VaultProtection.Hello => _loc.GetString("Settings.ProtectionDisplay.Hello"),
            VaultProtection.Volatile => _loc.GetString("Settings.ProtectionDisplay.Volatile"),
            _ => _loc.GetString("Common.Unknown"),
        };

        // The privacy panel's guarantee + "additional safeguards" lines, scoped to the running mode.
        AtRestGuarantee = protection switch
        {
            VaultProtection.DpapiOnly => _loc.GetString("Settings.Privacy.Guarantee.DpapiOnly"),
            VaultProtection.Passphrase => _loc.GetString("Settings.Privacy.Guarantee.Passphrase"),
            VaultProtection.Hello => _loc.GetString("Settings.Privacy.Guarantee.Hello"),
            VaultProtection.Volatile => _loc.GetString("Settings.Privacy.Guarantee.Volatile"),
            _ => _loc.GetString("Common.Unknown"),
        };

        ExtraProtection = protection switch
        {
            VaultProtection.DpapiOnly => _loc.GetString("Settings.Privacy.Extra.DpapiOnly"),
            VaultProtection.Passphrase => _loc.GetString("Settings.Privacy.Extra.Passphrase"),
            VaultProtection.Hello => _loc.GetString("Settings.Privacy.Extra.Hello"),
            VaultProtection.Volatile => _loc.GetString("Settings.Privacy.Extra.Volatile"),
            _ => string.Empty,
        };

        // A storage-mode change is pending when the selected setting differs from the running mode (it applies on restart).
        IsStorageRestartPending =
            (_settings.Current.Storage == StorageMode.VolatileMemory) != (protection == VaultProtection.Volatile);
    }

    // --- Storage mode two-way sync ---
    partial void OnIsEncryptedDiskChanged(bool value)
    {
        if (_isSyncing || !value)
        {
            return;
        }

        IsVolatileMemory = false;
        PersistStorage(StorageMode.EncryptedDisk);
    }

    partial void OnIsVolatileMemoryChanged(bool value)
    {
        if (_isSyncing || !value)
        {
            return;
        }

        IsEncryptedDisk = false;
        PersistStorage(StorageMode.VolatileMemory);
    }

    private void PersistStorage(StorageMode mode)
    {
        if (_settings.Current.Storage == mode)
        {
            return;
        }

        _settings.Update(_settings.Current with { Storage = mode });

        // The change applies on restart, so the privacy panel keeps showing the running mode; refresh so the
        // "pending restart" note (IsStorageRestartPending) appears or clears for the new selection.
        RefreshProtection();
    }

    // --- Language two-way sync ---
    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (_isSyncing || value is null || _settings.Current.Language == value.Value)
        {
            return;
        }

        // Persist only; the new language is applied on the next restart (like the other restart-applied settings).
        _settings.Update(_settings.Current with { Language = value.Value });
    }

    /// <summary>Sets or changes the passphrase.</summary>
    [RelayCommand]
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Best-effort UI command; failures are surfaced via DescribePassphraseFailure.")]
    private async Task SavePassphraseAsync()
    {
        ClearPassphraseMessages();

        if (string.IsNullOrEmpty(NewPassphrase))
        {
            PassphraseError = _loc.GetString("Settings.Msg.EnterNewPassphrase");
            OnPropertyChanged(nameof(HasPassphraseError));
            return;
        }

        if (!string.Equals(NewPassphrase, ConfirmPassphrase, StringComparison.Ordinal))
        {
            PassphraseError = _loc.GetString("Settings.Msg.PassphraseMismatch");
            OnPropertyChanged(nameof(HasPassphraseError));
            return;
        }

        IsPassphraseBusy = true;
        try
        {
            var current = HasPassphrase ? CurrentPassphrase : null;
            await _vault.SetOrChangePassphraseAsync(current, NewPassphrase);

            ClearPassphraseFields();
            PassphraseSuccess = _loc.GetString("Settings.Msg.PassphraseUpdated");
            OnPropertyChanged(nameof(HasPassphraseSuccess));
            RefreshProtection();
        }
        catch (Exception ex)
        {
            PassphraseError = DescribePassphraseFailure(ex);
            OnPropertyChanged(nameof(HasPassphraseError));
        }
        finally
        {
            IsPassphraseBusy = false;
        }
    }

    /// <summary>Removes passphrase protection and reverts to DPAPI only.</summary>
    [RelayCommand]
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Best-effort UI command; failures are surfaced via DescribePassphraseFailure.")]
    private async Task RemovePassphraseAsync()
    {
        ClearPassphraseMessages();

        if (string.IsNullOrEmpty(CurrentPassphrase))
        {
            PassphraseError = _loc.GetString("Settings.Msg.NeedCurrentToRemove");
            OnPropertyChanged(nameof(HasPassphraseError));
            return;
        }

        IsPassphraseBusy = true;
        try
        {
            await _vault.RemovePassphraseAsync(CurrentPassphrase);

            ClearPassphraseFields();
            PassphraseSuccess = _loc.GetString("Settings.Msg.PassphraseRemoved");
            OnPropertyChanged(nameof(HasPassphraseSuccess));
            RefreshProtection();
        }
        catch (Exception ex)
        {
            PassphraseError = DescribePassphraseFailure(ex);
            OnPropertyChanged(nameof(HasPassphraseError));
        }
        finally
        {
            IsPassphraseBusy = false;
        }
    }

    /// <summary>
    /// Protects with Windows Hello. If currently passphrase-protected, the current passphrase is
    /// required (not required for DPAPI only). On success the protection is replaced with Hello
    /// (it cannot be combined with a passphrase). The OS Hello prompt appears.
    /// </summary>
    [RelayCommand]
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Best-effort UI command; failures are surfaced via DescribeHelloFailure.")]
    private async Task EnableHelloAsync()
    {
        ClearPassphraseMessages();

        // Only while passphrase-protected, unlock the current key with the current passphrase before resealing with Hello.
        if (HasPassphrase && string.IsNullOrEmpty(CurrentPassphrase))
        {
            PassphraseError = _loc.GetString("Settings.Msg.NeedCurrentToSwitchHello");
            OnPropertyChanged(nameof(HasPassphraseError));
            return;
        }

        IsPassphraseBusy = true;
        try
        {
            var current = HasPassphrase ? CurrentPassphrase : null;
            await _vault.EnableHelloAsync(current);

            ClearPassphraseFields();
            PassphraseSuccess = _loc.GetString("Settings.Msg.HelloProtected");
            OnPropertyChanged(nameof(HasPassphraseSuccess));
            RefreshProtection();
        }
        catch (Exception ex)
        {
            PassphraseError = DescribeHelloFailure(ex);
            OnPropertyChanged(nameof(HasPassphraseError));
        }
        finally
        {
            IsPassphraseBusy = false;
        }
    }

    /// <summary>Disables Windows Hello protection and reverts to DPAPI only (the Hello prompt appears to authorize the change).</summary>
    [RelayCommand]
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Best-effort UI command; failures are surfaced via DescribeHelloFailure.")]
    private async Task DisableHelloAsync()
    {
        ClearPassphraseMessages();

        IsPassphraseBusy = true;
        try
        {
            await _vault.DisableHelloAsync();

            ClearPassphraseFields();
            PassphraseSuccess = _loc.GetString("Settings.Msg.HelloDisabled");
            OnPropertyChanged(nameof(HasPassphraseSuccess));
            RefreshProtection();
        }
        catch (Exception ex)
        {
            PassphraseError = DescribeHelloFailure(ex);
            OnPropertyChanged(nameof(HasPassphraseError));
        }
        finally
        {
            IsPassphraseBusy = false;
        }
    }

    private void ClearPassphraseMessages()
    {
        PassphraseError = string.Empty;
        PassphraseSuccess = string.Empty;
        OnPropertyChanged(nameof(HasPassphraseError));
        OnPropertyChanged(nameof(HasPassphraseSuccess));
    }

    private void ClearPassphraseFields()
    {
        CurrentPassphrase = string.Empty;
        NewPassphrase = string.Empty;
        ConfirmPassphrase = string.Empty;
    }

    /// <summary>Delegates confirmation to the window (a ContentDialog needs a XamlRoot).</summary>
    [RelayCommand]
    private void PanicWipe() => PanicWipeRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void AddExcludedApp()
    {
        var name = NewExcludedApp.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        // Normalize by stripping the .exe extension (the underlying comparison is OrdinalIgnoreCase).
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        if (ExcludedApps.Any(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            NewExcludedApp = string.Empty;
            return;
        }

        InsertSorted(name);
        NewExcludedApp = string.Empty;
        PersistExclusions();
    }

    [RelayCommand]
    private void RemoveExcludedApp(ExcludedAppViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var match = ExcludedApps.FirstOrDefault(
            a => string.Equals(a.Name, item.Name, StringComparison.OrdinalIgnoreCase));
        if (match is not null && ExcludedApps.Remove(match))
        {
            PersistExclusions();
        }
    }

    private void InsertSorted(string name)
    {
        var index = 0;
        while (index < ExcludedApps.Count
               && string.Compare(ExcludedApps[index].Name, name, StringComparison.OrdinalIgnoreCase) < 0)
        {
            index++;
        }

        ExcludedApps.Insert(index, new ExcludedAppViewModel(name));
    }

    private void PersistExclusions()
    {
        var set = new HashSet<string>(ExcludedApps.Select(a => a.Name), StringComparer.OrdinalIgnoreCase);
        _settings.Update(_settings.Current with { ExcludedProcessNames = set });
    }

    // --- Masking ---
    partial void OnMaskGenericPasswordsChanged(bool value)
    {
        if (_isSyncing)
        {
            return;
        }

        _settings.Update(_settings.Current with { MaskGenericPasswords = value });
    }

    partial void OnStripTrackingParametersChanged(bool value)
    {
        if (_isSyncing)
        {
            return;
        }

        _settings.Update(_settings.Current with { StripTrackingParameters = value });
    }

    // --- Retention ---
    partial void OnMaxAgeDaysChanged(int value)
    {
        // Propagate the change to the double proxy (the NumberBox binding target) as well.
        OnPropertyChanged(nameof(MaxAgeDaysValue));

        if (_isSyncing)
        {
            return;
        }

        // Clamp to the lower bound so a negative/zero value from the NumberBox does not break things.
        if (value < 1)
        {
            MaxAgeDays = 1;
            return;
        }

        _settings.Update(_settings.Current with { MaxAgeDays = value });
    }

    partial void OnMaxEntriesChanged(int value)
    {
        OnPropertyChanged(nameof(MaxEntriesValue));

        if (_isSyncing)
        {
            return;
        }

        if (value < 1)
        {
            MaxEntries = 1;
            return;
        }

        _settings.Update(_settings.Current with { MaxEntries = value });
    }

    // --- Auto-start ---
    partial void OnRunAtStartupChanged(bool value)
    {
        if (_isSyncing)
        {
            return;
        }

        _startup.SetEnabled(value);
        _settings.Update(_settings.Current with { RunAtStartup = value });
    }
}
