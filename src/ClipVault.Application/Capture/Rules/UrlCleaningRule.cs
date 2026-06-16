using System.Text;
using ClipVault.Application.Abstractions;
using ClipVault.Application.Insights;
using ClipVault.Domain.ValueObjects;

namespace ClipVault.Application.Capture.Rules;

/// <summary>
/// When enabled in settings, strips tracking parameters from a captured URL so the stored entry (and any
/// paste-back) is clean. Only single-URL text clips are affected; arbitrary text is left unchanged. The OS
/// clipboard itself is never rewritten.
/// </summary>
public sealed class UrlCleaningRule(ISettingsService settings) : ICaptureRule
{
    /// <inheritdoc/>
    public CaptureRuleResult Evaluate(ClipboardSnapshot snapshot)
    {
        if (snapshot.Type != ClipContentType.Text || !settings.Current.StripTrackingParameters)
        {
            return CaptureRuleResult.Continue(snapshot);
        }

        var text = Encoding.UTF8.GetString(snapshot.Payload);
        if (!UrlTrackingStripper.TryStrip(text, out var cleaned))
        {
            return CaptureRuleResult.Continue(snapshot);
        }

        return CaptureRuleResult.Continue(snapshot with
        {
            Payload = Encoding.UTF8.GetBytes(cleaned),
            Preview = TextPreview.Create(cleaned),
        });
    }
}
