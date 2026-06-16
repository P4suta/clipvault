using ClipVault.Application.Capture;
using ClipVault.Application.Capture.Classifiers;
using ClipVault.Application.Capture.Rules;
using ClipVault.Application.Clipboard;
using ClipVault.Application.DependencyInjection;
using ClipVault.Application.History;
using ClipVault.Application.Retention;
using Microsoft.Extensions.DependencyInjection;

namespace ClipVault.Application.Tests;

public class ApplicationCompositionTests
{
    [Fact]
    public void Registers_the_capture_rules_in_evaluation_order()
    {
        var ruleTypes = Build()
            .Where(d => d.ServiceType == typeof(ICaptureRule))
            .Select(d => d.ImplementationType!)
            .ToList();

        Assert.Equal(
            new[]
            {
                typeof(PrivacySignalRule),
                typeof(SourceAppRule),
                typeof(CaptureStateRule),
                typeof(ContentClassificationRule),
                typeof(UrlCleaningRule),
                typeof(SizeRule),
            },
            ruleTypes);
    }

    [Fact]
    public void Registers_all_five_content_classifiers()
    {
        var classifiers = Build()
            .Where(d => d.ServiceType == typeof(IClipboardContentClassifier))
            .Select(d => d.ImplementationType!)
            .ToList();

        Assert.Equal(5, classifiers.Count);
        Assert.Contains(typeof(ApiKeyClassifier), classifiers);
        Assert.Contains(typeof(CreditCardClassifier), classifiers);
        Assert.Contains(typeof(GenericPasswordClassifier), classifiers);
        Assert.Contains(typeof(JwtClassifier), classifiers);
        Assert.Contains(typeof(PemPrivateKeyClassifier), classifiers);
    }

    [Theory]
    [InlineData(typeof(CaptureGate))]
    [InlineData(typeof(ClipboardIngestionService))]
    [InlineData(typeof(ClipboardActionService))]
    [InlineData(typeof(HistoryQueryService))]
    [InlineData(typeof(RetentionService))]
    public void Registers_core_services_as_singletons(Type serviceType)
    {
        var descriptor = Assert.Single(Build(), d => d.ServiceType == serviceType);

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    private static IServiceCollection Build() => new TestServiceCollection().AddApplication();

    // IServiceCollection is a marker interface over IList<ServiceDescriptor>; this avoids referencing the
    // concrete Microsoft.Extensions.DependencyInjection package (only the Abstractions are available here).
    private sealed class TestServiceCollection : List<ServiceDescriptor>, IServiceCollection
    {
    }
}
