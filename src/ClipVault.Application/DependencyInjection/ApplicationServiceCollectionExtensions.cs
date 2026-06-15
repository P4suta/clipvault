using ClipVault.Application.Abstractions;
using ClipVault.Application.Capture;
using ClipVault.Application.Capture.Classifiers;
using ClipVault.Application.Capture.Rules;
using ClipVault.Application.Clipboard;
using ClipVault.Application.History;
using ClipVault.Application.Retention;
using ClipVault.Application.Settings;
using ClipVault.Domain.Policies;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace ClipVault.Application.DependencyInjection;

/// <summary>Dependency injection extensions for registering the application layer services.</summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Registers the application-layer services: the capture pipeline, search, retention, and so on.</summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <returns>The same service collection, to allow chaining.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        services.AddSingleton<ISettingsService, InMemorySettingsService>();
        services.AddSingleton<ICaptureStateService, CaptureStateService>();

        // Retention policy (settings come from the Domain defaults).
        services.AddSingleton(RetentionSettings.Default);
        services.AddSingleton<IRetentionPolicy, DefaultRetentionPolicy>();

        // Content classifiers (registration order does not matter because evaluation is rejection-first).
        services.AddSingleton<IClipboardContentClassifier, ApiKeyClassifier>();
        services.AddSingleton<IClipboardContentClassifier, PemPrivateKeyClassifier>();
        services.AddSingleton<IClipboardContentClassifier, JwtClassifier>();
        services.AddSingleton<IClipboardContentClassifier, CreditCardClassifier>();
        services.AddSingleton<IClipboardContentClassifier, GenericPasswordClassifier>();

        // Privacy gate rules (evaluated in this registration order).
        services.AddSingleton<ICaptureRule, PrivacySignalRule>();
        services.AddSingleton<ICaptureRule, SourceAppRule>();
        services.AddSingleton<ICaptureRule, CaptureStateRule>();
        services.AddSingleton<ICaptureRule, ContentClassificationRule>();
        services.AddSingleton<ICaptureRule, SizeRule>();
        services.AddSingleton<CaptureGate>();

        services.AddSingleton<ClipboardIngestionService>();
        services.AddSingleton<ClipboardActionService>();
        services.AddSingleton<HistoryQueryService>();
        services.AddSingleton<RetentionService>();

        return services;
    }
}
