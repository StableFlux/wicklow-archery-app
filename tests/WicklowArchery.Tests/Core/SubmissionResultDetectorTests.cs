using WicklowArchery.Core.Forms;
using Xunit;

namespace WicklowArchery.Tests.Core;

public class SubmissionResultDetectorTests
{
    [Fact]
    public void Classify_OkFixture_IsSuccess() =>
        Assert.Equal(SubmissionOutcome.Success, SubmissionResultDetector.Classify(200, Fixtures.Read("test-form-response-ok.html")));

    [Fact]
    public void Classify_ErrorFixture_IsRejected() =>
        Assert.Equal(SubmissionOutcome.Rejected, SubmissionResultDetector.Classify(400, Fixtures.Read("test-form-response-error.html")));

    [Fact]
    public void Classify_FormPageReturned_IsNotSuccess() =>
        Assert.NotEqual(SubmissionOutcome.Success, SubmissionResultDetector.Classify(200, Fixtures.Read("test-form-2026-11-09.html")));

    [Fact]
    public void Classify_FormPageReturned_IsRejected() =>
        Assert.Equal(SubmissionOutcome.Rejected, SubmissionResultDetector.Classify(200, Fixtures.Read("test-form-2026-11-09.html")));

    [Fact]
    public void Classify_SuccessTextWith400_IsNotSuccess() =>
        Assert.NotEqual(SubmissionOutcome.Success, SubmissionResultDetector.Classify(400, "Your response has been recorded."));

    [Fact]
    public void Classify_ServerError_IsUnexpected() =>
        Assert.Equal(SubmissionOutcome.Unexpected, SubmissionResultDetector.Classify(500, ""));

    [Fact]
    public void Classify_EmptyOk_IsUnexpected() =>
        Assert.Equal(SubmissionOutcome.Unexpected, SubmissionResultDetector.Classify(200, ""));
}
