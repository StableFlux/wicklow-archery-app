using System;
using WicklowArchery.Core.Data;
using WicklowArchery.Core.Net;
using Xunit;

namespace WicklowArchery.Tests.Core;

public class FormAddressTests
{
    [Fact]
    public void IsAllowed_TestAndClubFormUrls_True()
    {
        Assert.True(FormAddress.IsAllowed(new Uri(FormUrls.TestForm)));
        Assert.True(FormAddress.IsAllowed(new Uri(FormUrls.ClubForm)));
        Assert.True(FormAddress.IsAllowed(new Uri(FormUrls.TestForm.Replace("/viewform", "/formResponse"))));
    }

    [Fact]
    public void IsAllowed_HttpScheme_False() =>
        Assert.False(FormAddress.IsAllowed(new Uri(FormUrls.TestForm.Replace("https:", "http:"))));

    [Fact]
    public void IsAllowed_UserInfo_False() =>
        Assert.False(FormAddress.IsAllowed(new Uri("https://evil@docs.google.com/forms/d/e/x/viewform")));

    [Fact]
    public void IsAllowed_OtherHost_False()
    {
        Assert.False(FormAddress.IsAllowed(new Uri("https://docs.google.com.evil.example/forms/d/e/x/viewform")));
        Assert.False(FormAddress.IsAllowed(new Uri("https://evil.example/forms/d/e/x/formResponse")));
    }

    [Fact]
    public void FormId_ReadsIdFromViewformAndFormResponse()
    {
        Assert.Equal("abc123", FormAddress.FormId(new Uri("https://docs.google.com/forms/d/e/abc123/viewform")));
        Assert.Equal("abc123", FormAddress.FormId(new Uri("https://docs.google.com/forms/d/e/abc123/formResponse")));
        Assert.Null(FormAddress.FormId(new Uri("https://docs.google.com/spreadsheets/d/e/abc123/viewform")));
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("https://forms.gle/abc")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_GarbageOrShortLink_Null(string? text) => Assert.Null(FormAddress.TryParse(text));
}
