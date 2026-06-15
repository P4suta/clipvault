namespace ClipVault.Application.Capture.Classifiers;

/// <summary>The handling indicated by a classifier (capture, mask, or reject).</summary>
public enum ClassificationAction
{
    /// <summary>Not sensitive. Capture it as-is.</summary>
    Allow,

    /// <summary>Mask the sensitive part and capture it.</summary>
    Mask,

    /// <summary>Sensitive, so do not capture it.</summary>
    Reject,
}
