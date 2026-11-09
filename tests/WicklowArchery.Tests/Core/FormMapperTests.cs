using System;
using System.Linq;
using WicklowArchery.Core.Forms;
using Xunit;

namespace WicklowArchery.Tests.Core;

public class FormMapperTests
{
    private static FormDefinition Club() => FormParser.Parse(Fixtures.Read("club-form-2026-11-09.html"));
    private static FormDefinition Test() => FormParser.Parse(Fixtures.Read("test-form-2026-11-09.html"));

    private static FormDefinition Without(FormDefinition d, string titleContains) =>
        d with { Questions = d.Questions.Where(q => !q.Title.Contains(titleContains, StringComparison.OrdinalIgnoreCase)).ToList() };

    private static FormDefinition Replace(FormDefinition d, string titleContains, Func<FormQuestion, FormQuestion> change) =>
        d with { Questions = d.Questions.Select(q => q.Title.Contains(titleContains, StringComparison.OrdinalIgnoreCase) ? change(q) : q).ToList() };

    [Fact]
    public void Map_ClubSnapshot_IsUsable()
    {
        var check = FormMapper.Map(Club());
        Assert.True(check.IsUsable, string.Join("; ", check.Problems));
        var form = check.Form!;
        Assert.Equal(3, form.Sessions.Count);
        Assert.Equal(4, form.PaymentOptions.Count);
        Assert.Equal(4, form.LoanOptions.Count);
        Assert.Equal("wicklow-archers.sumup.link", form.SumUpUrl!.Host);
        Assert.Equal(1741681513L, form.VenueEntry);
    }

    [Fact]
    public void Map_TestSnapshot_IsUsable()
    {
        var check = FormMapper.Map(Test());
        Assert.True(check.IsUsable, string.Join("; ", check.Problems));
        Assert.Equal(2011193388L, check.Form!.VenueEntry);
        Assert.NotNull(check.Form.SumUpUrl);
    }

    [Fact]
    public void Map_FullNameRemoved_ReportsMissing()
    {
        var check = FormMapper.Map(Without(Club(), "full name"));
        Assert.False(check.IsUsable);
        Assert.Contains("Can't find the \"Full Name\" question on the form.", check.Problems);
    }

    [Fact]
    public void Map_NewRequiredQuestion_Reported()
    {
        var d = Club();
        d = d with { Questions = d.Questions.Append(new FormQuestion(1, "Date of birth", null, QuestionType.ShortText, 99, true, [])).ToList() };
        var check = FormMapper.Map(d);
        Assert.False(check.IsUsable);
        Assert.Contains("The form has a new required question: \"Date of birth\".", check.Problems);
    }

    [Fact]
    public void Map_NewOptionalQuestion_Ignored()
    {
        var d = Club();
        d = d with { Questions = d.Questions.Append(new FormQuestion(1, "Date of birth", null, QuestionType.ShortText, 99, false, [])).ToList() };
        Assert.True(FormMapper.Map(d).IsUsable);
    }

    [Fact]
    public void Map_VenueBecameRadio_ReportsChangedType()
    {
        var check = FormMapper.Map(Replace(Club(), "venue", q => q with { Type = QuestionType.Radio }));
        Assert.False(check.IsUsable);
        Assert.Null(check.Form);
        Assert.Contains("The \"Venue options\" question has changed type.", check.Problems);
    }

    [Fact]
    public void Map_LoanBecameRequired_Reported()
    {
        var check = FormMapper.Map(Replace(Club(), "loan", q => q with { Required = true }));
        Assert.False(check.IsUsable);
        Assert.Contains("\"LOAN EQUIPMENT\" is now required.", check.Problems);
    }

    [Fact]
    public void Map_NoPaymentLink_StillUsableWithNullSumUp()
    {
        var check = FormMapper.Map(Without(Club(), "payment link"));
        Assert.True(check.IsUsable, string.Join("; ", check.Problems));
        Assert.Null(check.Form!.SumUpUrl);
    }
}
