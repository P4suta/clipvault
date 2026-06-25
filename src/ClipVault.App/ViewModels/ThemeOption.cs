using ClipVault.Application.Abstractions;

namespace ClipVaultApp.ViewModels;

/// <summary>A selectable UI theme: the persisted value plus the name shown in the picker.</summary>
/// <param name="Value">The theme stored in settings.</param>
/// <param name="DisplayName">The localized label shown in the combo box.</param>
public sealed record ThemeOption(AppTheme Value, string DisplayName);
