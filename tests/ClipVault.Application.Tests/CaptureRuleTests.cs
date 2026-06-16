using System.Text;
using ClipVault.Application.Abstractions;
using ClipVault.Application.Capture;
using ClipVault.Application.Capture.Classifiers;
using ClipVault.Application.Capture.Rules;
using ClipVault.Application.Settings;
using ClipVault.Domain.ValueObjects;

namespace ClipVault.Application.Tests;

public class CaptureRuleTests
{
    [Fact]
    public void PrivacySignalRule_rejects_when_os_forbids()
    {
        var rule = new PrivacySignalRule();
        var forbidden = Snapshots.Text("secret", signals: new ClipboardPrivacySignals(ExcludeFromHistory: true, null));

        Assert.True(rule.Evaluate(forbidden).Rejected);
        Assert.False(rule.Evaluate(Snapshots.Text("ok")).Rejected);
    }

    [Fact]
    public void SourceAppRule_rejects_excluded_process()
    {
        var rule = new SourceAppRule(new InMemorySettingsService());
        var fromKeePass = Snapshots.Text("pw", source: new SourceApplication("KeePass", null, null));

        Assert.True(rule.Evaluate(fromKeePass).Rejected);
        Assert.False(rule.Evaluate(Snapshots.Text("x", new SourceApplication("notepad", null, null))).Rejected);
    }

    [Fact]
    public void CaptureStateRule_rejects_while_paused()
    {
        var state = new CaptureStateService();
        var rule = new CaptureStateRule(state);

        Assert.False(rule.Evaluate(Snapshots.Text("x")).Rejected);
        state.Pause();
        Assert.True(rule.Evaluate(Snapshots.Text("x")).Rejected);
    }

    [Fact]
    public void SizeRule_rejects_oversized_image()
    {
        var settings = new InMemorySettingsService();
        settings.Update(ClipVaultSettings.Default with { MaxImageBytes = 10 });
        var rule = new SizeRule(settings);

        Assert.True(rule.Evaluate(Snapshots.Image(byteCount: 20)).Rejected);
        Assert.False(rule.Evaluate(Snapshots.Image(byteCount: 5)).Rejected);
    }

    [Fact]
    public void ContentClassificationRule_rejects_api_key()
    {
        var rule = new ContentClassificationRule([new ApiKeyClassifier()]);
        var result = rule.Evaluate(Snapshots.Text("key sk-abcdefghijklmnopqrstuvwxyz0123"));

        Assert.True(result.Rejected);
    }

    [Fact]
    public void ContentClassificationRule_masks_credit_card_and_rewrites_payload()
    {
        var rule = new ContentClassificationRule([new CreditCardClassifier()]);
        var result = rule.Evaluate(Snapshots.Text("card 4111 1111 1111 1111"));

        Assert.False(result.Rejected);
        var newText = Encoding.UTF8.GetString(result.Snapshot!.Payload);
        Assert.Contains("•", newText, StringComparison.Ordinal);
        Assert.DoesNotContain("4111 1111 1111 1111", newText, StringComparison.Ordinal);
    }

    [Fact]
    public void Gate_rejects_on_first_failing_rule()
    {
        var gate = new CaptureGate([new PrivacySignalRule(), new CaptureStateRule(new CaptureStateService())]);
        var forbidden = Snapshots.Text("x", signals: new ClipboardPrivacySignals(true, null));

        var result = gate.Evaluate(forbidden);

        Assert.False(result.IsAccepted);
        Assert.NotNull(result.RejectionReason);
    }

    [Fact]
    public void Gate_accepts_and_returns_possibly_transformed_snapshot()
    {
        var gate = new CaptureGate([new ContentClassificationRule([new CreditCardClassifier()])]);

        var result = gate.Evaluate(Snapshots.Text("card 4111 1111 1111 1111"));

        Assert.True(result.IsAccepted);
        Assert.DoesNotContain(
            "4111 1111 1111 1111", Encoding.UTF8.GetString(result.Snapshot!.Payload), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, null, true)] // ExcludeFromHistory forbids capture.
    [InlineData(false, false, true)] // CanIncludeInHistory == false forbids capture.
    [InlineData(false, true, false)] // Explicitly allowed.
    [InlineData(false, null, false)] // No signal at all.
    [InlineData(true, true, true)] // ExcludeFromHistory wins over CanIncludeInHistory.
    public void PrivacySignalRule_honours_os_signals(bool exclude, bool? canInclude, bool expectedRejected)
    {
        var rule = new PrivacySignalRule();
        var snapshot = Snapshots.Text("x", signals: new ClipboardPrivacySignals(exclude, canInclude));

        Assert.Equal(expectedRejected, rule.Evaluate(snapshot).Rejected);
    }

    [Theory]
    [InlineData("KeePass", true)]
    [InlineData("keepass", true)] // Case-insensitive match against the default exclusions.
    [InlineData("1Password", true)]
    [InlineData("BITWARDEN", true)]
    [InlineData("notepad", false)]
    [InlineData("chrome", false)]
    public void SourceAppRule_excludes_by_case_insensitive_name(string process, bool expectedRejected)
    {
        var rule = new SourceAppRule(new InMemorySettingsService());
        var snapshot = Snapshots.Text("x", source: new SourceApplication(process, null, null));

        Assert.Equal(expectedRejected, rule.Evaluate(snapshot).Rejected);
    }

    [Fact]
    public void SourceAppRule_allows_everything_when_exclusion_list_is_empty()
    {
        var settings = new InMemorySettingsService();
        settings.Update(ClipVaultSettings.Default with { ExcludedProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) });
        var rule = new SourceAppRule(settings);

        Assert.False(rule.Evaluate(Snapshots.Text("x", source: new SourceApplication("keepass", null, null))).Rejected);
    }

    [Fact]
    public void SizeRule_enforces_image_byte_limit_at_the_boundary()
    {
        var settings = new InMemorySettingsService();
        settings.Update(ClipVaultSettings.Default with { MaxImageBytes = 10 });
        var rule = new SizeRule(settings);

        Assert.True(rule.Evaluate(Snapshots.Image(byteCount: 11)).Rejected); // Over the limit.
        Assert.False(rule.Evaluate(Snapshots.Image(byteCount: 10)).Rejected); // Exactly at the limit (boundary is exclusive).
        Assert.False(rule.Evaluate(Snapshots.Image(byteCount: 9)).Rejected); // Under the limit.
    }

    [Fact]
    public void SizeRule_never_rejects_text_regardless_of_size()
    {
        var settings = new InMemorySettingsService();
        settings.Update(ClipVaultSettings.Default with { MaxImageBytes = 10 });
        var rule = new SizeRule(settings);

        Assert.False(rule.Evaluate(Snapshots.Text(new string('a', 1000))).Rejected);
    }

    [Fact]
    public void ContentClassificationRule_reject_takes_precedence_over_mask()
    {
        // A credit card would be masked and an API key rejected; rejection must win regardless of classifier order.
        const string text = "4111 1111 1111 1111 and sk-abcdefghijklmnopqrstuvwx";

        Assert.True(new ContentClassificationRule([new CreditCardClassifier(), new ApiKeyClassifier()])
            .Evaluate(Snapshots.Text(text)).Rejected);
        Assert.True(new ContentClassificationRule([new ApiKeyClassifier(), new CreditCardClassifier()])
            .Evaluate(Snapshots.Text(text)).Rejected);
    }

    [Fact]
    public void ContentClassificationRule_reject_reason_names_the_classifier()
    {
        var rule = new ContentClassificationRule([new ApiKeyClassifier()]);

        var result = rule.Evaluate(Snapshots.Text("sk-abcdefghijklmnopqrstuvwx"));

        Assert.True(result.Rejected);
        Assert.Contains("ApiKey", result.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentClassificationRule_masks_payload_and_preview_so_nothing_leaks()
    {
        var rule = new ContentClassificationRule([new CreditCardClassifier()]);

        var result = rule.Evaluate(Snapshots.Text("card 4111 1111 1111 1111 end"));

        Assert.False(result.Rejected);
        var payloadText = Encoding.UTF8.GetString(result.Snapshot!.Payload);
        Assert.DoesNotContain("4111 1111 1111 1111", payloadText, StringComparison.Ordinal);

        // The preview is stored unencrypted, so the secret must be masked there too.
        Assert.DoesNotContain("4111 1111 1111 1111", result.Snapshot.Preview, StringComparison.Ordinal);
        Assert.Contains("•", result.Snapshot.Preview, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentClassificationRule_ignores_non_text_content()
    {
        var rule = new ContentClassificationRule([new ApiKeyClassifier(), new CreditCardClassifier()]);
        var image = Snapshots.Image(byteCount: 50);

        var result = rule.Evaluate(image);

        Assert.False(result.Rejected);
        Assert.Same(image, result.Snapshot);
    }

    [Fact]
    public void ContentClassificationRule_scans_the_entire_text()
    {
        var rule = new ContentClassificationRule([new ApiKeyClassifier()]);

        // A secret far past the old 4096-char window is now detected: the full text is scanned.
        var atEnd = new string('a', 8192) + " sk-abcdefghijklmnopqrstuvwx";
        Assert.True(rule.Evaluate(Snapshots.Text(atEnd)).Rejected);

        // And one at the very start is still detected.
        var atStart = "sk-abcdefghijklmnopqrstuvwx " + new string('a', 8192);
        Assert.True(rule.Evaluate(Snapshots.Text(atStart)).Rejected);
    }

    [Fact]
    public void Gate_short_circuits_after_the_first_rejection()
    {
        var gate = new CaptureGate([new RejectingRule("nope"), new ThrowingRule()]);

        var result = gate.Evaluate(Snapshots.Text("x"));

        Assert.False(result.IsAccepted);
        Assert.Equal("nope", result.RejectionReason);
    }

    [Fact]
    public void Gate_chains_transformations_between_rules()
    {
        var gate = new CaptureGate([new AppendRule("-A"), new AppendRule("-B")]);

        var result = gate.Evaluate(Snapshots.Text("x"));

        Assert.True(result.IsAccepted);
        Assert.Equal("x-A-B", Encoding.UTF8.GetString(result.Snapshot!.Payload));
    }

    [Fact]
    public void Gate_accepts_the_unchanged_snapshot_when_there_are_no_rules()
    {
        var snapshot = Snapshots.Text("x");

        var result = new CaptureGate([]).Evaluate(snapshot);

        Assert.True(result.IsAccepted);
        Assert.Same(snapshot, result.Snapshot);
    }

    private sealed class RejectingRule(string reason) : ICaptureRule
    {
        public CaptureRuleResult Evaluate(ClipboardSnapshot snapshot) => CaptureRuleResult.Reject(reason);
    }

    private sealed class ThrowingRule : ICaptureRule
    {
        public CaptureRuleResult Evaluate(ClipboardSnapshot snapshot) =>
            throw new InvalidOperationException("A rule after a rejection must not be evaluated.");
    }

    private sealed class AppendRule(string marker) : ICaptureRule
    {
        public CaptureRuleResult Evaluate(ClipboardSnapshot snapshot) =>
            CaptureRuleResult.Continue(snapshot with
            {
                Payload = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(snapshot.Payload) + marker),
            });
    }
}
