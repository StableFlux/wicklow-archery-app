using System;

namespace WicklowArchery.Core.Forms;

public enum SubmissionOutcome { Success, Rejected, Unexpected }

public static class SubmissionResultDetector
{
    /// <summary>Shown by Google on an accepted response (measured in submit-findings.md).</summary>
    public const string SuccessMarker = "Your response has been recorded";

    /// <summary>Present only when Google re-displays the form, i.e. the response was not accepted.</summary>
    public const string FormRedisplayMarker = "name=\"fbzx\"";

    public static SubmissionOutcome Classify(int statusCode, string body)
    {
        body ??= "";
        var hasSuccess = body.Contains(SuccessMarker, StringComparison.Ordinal);
        if (statusCode == 200 && hasSuccess) return SubmissionOutcome.Success;
        if (statusCode == 400) return SubmissionOutcome.Rejected;
        if (statusCode == 200 && !hasSuccess && body.Contains(FormRedisplayMarker, StringComparison.Ordinal))
            return SubmissionOutcome.Rejected;
        return SubmissionOutcome.Unexpected;
    }
}
