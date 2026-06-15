using ClipVault.Application.Abstractions;

namespace ClipVaultApp.ViewModels;

/// <summary>A selectable UI language: the persisted value plus the name shown in the picker.</summary>
/// <param name="Value">The language stored in settings.</param>
/// <param name="DisplayName">The label shown in the combo box (own-language endonym, or a localized "System default").</param>
public sealed record LanguageOption(AppLanguage Value, string DisplayName);
