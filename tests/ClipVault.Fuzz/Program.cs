using System.Text;
using ClipVault.Application.Abstractions;
using ClipVault.Application.Capture.Classifiers;
using ClipVault.Application.Capture.Rules;
using ClipVault.Application.Insights;
using ClipVault.Domain.ValueObjects;
using SharpFuzz;

namespace ClipVault.Fuzz;

// libFuzzer entry point for ClipVault.Application's untrusted-input parsing surface. Clipboard
// content is attacker-influenced, so the secret-detection classifiers and the insight parsers must
// never hang (ReDoS) or throw on hostile input. A single fuzz iteration drives every parser, so
// libFuzzer's coverage feedback explores all of them from one corpus; the goal is to surface
// regex catastrophic backtracking, unhandled exceptions, and decoding edge cases.
internal static class Program
{
    // Generic-password masking is opt-in; enable it so the entropy path is exercised.
    private static readonly ISettingsService Settings =
        new FixedSettingsService(ClipVaultSettings.Default with { MaskGenericPasswords = true });

    // The five classifiers wired exactly as the capture pipeline composes them.
    private static readonly ContentClassificationRule ClassificationRule = new(
        new IClipboardContentClassifier[]
        {
            new ApiKeyClassifier(),
            new JwtClassifier(),
            new CreditCardClassifier(),
            new GenericPasswordClassifier(Settings),
            new PemPrivateKeyClassifier(),
        });

    private static void Main() => Fuzzer.LibFuzzer.Run(FuzzOne);

    private static void FuzzOne(ReadOnlySpan<byte> data)
    {
        var bytes = data.ToArray();

        // Secret-detection chain: each classifier decodes the payload as UTF-8 and runs its regex.
        var snapshot = new ClipboardSnapshot(
            ClipContentType.Text,
            bytes,
            Preview: string.Empty,
            Image: null,
            Source: SourceApplication.Unknown,
            PrivacySignals: ClipboardPrivacySignals.None);
        ClassificationRule.Evaluate(snapshot);

        // Insight parsers operate on the decoded text directly (JSON reformat, URL tracking strip,
        // content-kind heuristics) — each is regex- or parser-backed and reachable from raw clipboard text.
        var text = Encoding.UTF8.GetString(bytes);
        JsonReformatter.TryFormat(text, indented: true, out _);
        JsonReformatter.TryFormat(text, indented: false, out _);
        UrlTrackingStripper.TryStrip(text, out _);
        ContentInsightService.ClassifyText(text);
    }
}

// Minimal fixed settings for the harness (the classifier chain only reads ISettingsService.Current).
internal sealed class FixedSettingsService(ClipVaultSettings settings) : ISettingsService
{
    public event EventHandler? Changed;

    public ClipVaultSettings Current { get; } = settings;

    public void Update(ClipVaultSettings settings) => Changed?.Invoke(this, EventArgs.Empty);
}
