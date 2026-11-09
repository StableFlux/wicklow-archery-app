using System;
using WicklowArchery.Core.Forms;
using Xunit;

namespace WicklowArchery.Tests.Core;

public class SessionTextTests
{
    [Theory]
    [InlineData("Monday 9th - St Gerards Hall - Training - 7pm - Two senior members must be in attendance", DayOfWeek.Monday)]
    [InlineData("Wednesday 11th - St Gerards Hall - Training- doors open at 6.45pm", DayOfWeek.Wednesday)]
    [InlineData("Sunday 15th - Dargle Valley - Gates open 9.30am.", DayOfWeek.Sunday)]
    [InlineData("Wed 11th - Hall", DayOfWeek.Wednesday)]
    [InlineData("Thurs 12th", DayOfWeek.Thursday)]
    [InlineData("Sunday's league", DayOfWeek.Sunday)]
    [InlineData("Monday or Tuesday", DayOfWeek.Monday)]
    public void DayOf_FindsDayWord(string sessionText, DayOfWeek expected)
    {
        Assert.Equal(expected, SessionText.DayOf(sessionText));
    }

    [Theory]
    [InlineData("Club BBQ at the hall")]
    [InlineData("Wedding fair")]
    public void DayOf_NoDayWord_ReturnsNull(string sessionText)
    {
        Assert.Null(SessionText.DayOf(sessionText));
    }

    [Fact]
    public void Split_SplitsAtFirstDashIntoHeadlineAndDetail()
    {
        var result = SessionText.Split("Monday 9th - St Gerards Hall - Training - 7pm - Two senior members must be in attendance");

        Assert.Equal(("Monday 9th", "St Gerards Hall - Training - 7pm - Two senior members must be in attendance"), result);
    }

    [Fact]
    public void Split_TrimsHeadlineWhenDashHasNoSpaceBeforeIt()
    {
        var result = SessionText.Split("Wednesday 11th - St Gerards Hall - Training- doors open at 6.45pm");

        Assert.Equal("Wednesday 11th", result.Headline);
    }

    [Fact]
    public void Split_NoDash_ReturnsWholeTextAsHeadlineWithEmptyDetail()
    {
        Assert.Equal(("Club BBQ", ""), SessionText.Split("Club BBQ"));
    }
}
