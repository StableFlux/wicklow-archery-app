using System;
using System.Linq;
using WicklowArchery.Core.Forms;
using Xunit;

namespace WicklowArchery.Tests.Core;

public class FormParserTests
{
    private static FormDefinition Club() => FormParser.Parse(Fixtures.Read("club-form-2026-11-09.html"));
    private static FormDefinition Test() => FormParser.Parse(Fixtures.Read("test-form-2026-11-09.html"));

    [Fact]
    public void Parse_ClubSnapshot_ReadsTitleAndSevenQuestions()
    {
        var form = Club();
        Assert.Equal("Wicklow Archers Venue Registration form 2026", form.Title);
        Assert.Equal(7, form.Questions.Count);
        Assert.Equal(new Uri("https://docs.google.com/forms/d/e/1FAIpQLSd9iDEkYbY5u7s3Eg0oDgHhk5RTTkPLQpri3-r6eR6_bYCiDw/formResponse"),
            form.ResponseUrl);
    }

    [Fact]
    public void Parse_ClubSnapshot_ReadsVenueQuestion()
    {
        var q = Club().Questions.Single(x => x.Title.StartsWith("Venue options"));
        Assert.Equal(QuestionType.Checkbox, q.Type);
        Assert.Equal(1741681513L, q.EntryId);
        Assert.True(q.Required);
        Assert.Equal(new[]
        {
            "Monday 9th - St Gerards Hall - Training - 7pm - Two senior members must be in attendance",
            "Wednesday 11th - St Gerards Hall - Training- doors open at 6.45pm",
            "Sunday 15th - Dargle Valley - Gates open 9.30am."
        }, q.Options);
    }

    [Fact]
    public void Parse_ClubSnapshot_LoanIsOptionalRadio()
    {
        var q = Club().Questions.Single(x => x.Title.Contains("LOAN"));
        Assert.Equal(QuestionType.Radio, q.Type);
        Assert.False(q.Required);
        Assert.Equal(4, q.Options.Count);
    }

    [Fact]
    public void Parse_ClubSnapshot_PaymentLinkIsImageWithUrl()
    {
        var q = Club().Questions.Single(x => x.Type == QuestionType.Image);
        Assert.Equal("https://wicklow-archers.sumup.link", q.Description);
        Assert.Null(q.EntryId);
    }

    [Fact]
    public void Parse_TestSnapshot_HasOwnEntryIds()
    {
        var form = Test();
        Assert.Equal(353097525L, form.Questions.Single(x => x.Title.StartsWith("Full Name")).EntryId);
        Assert.Contains(form.Questions, x => x.Type == QuestionType.TextBlock);
    }

    [Fact]
    public void Parse_ClubSnapshot_ReadsHiddenFields()
    {
        var fields = Club().HiddenFields;
        Assert.Contains(new("fbzx", "8688713072168764879"), fields);
        Assert.Contains(new("fvv", "1"), fields);
        Assert.Contains(new("pageHistory", "0"), fields);
        Assert.Contains(new("partialResponse", "[null,null,\"8688713072168764879\"]"), fields);
        Assert.DoesNotContain(fields, f => f.Key.EndsWith("_sentinel"));
    }

    [Fact]
    public void Parse_PageWithoutLoadData_Throws()
    {
        Assert.Throws<FormParseException>(() => FormParser.Parse("<html></html>"));
    }
}
