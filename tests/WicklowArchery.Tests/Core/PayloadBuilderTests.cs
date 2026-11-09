using System;
using System.Linq;
using WicklowArchery.Core.Forms;
using Xunit;

namespace WicklowArchery.Tests.Core;

public class PayloadBuilderTests
{
    private static RegistrationForm Form() =>
        FormMapper.Map(FormParser.Parse(Fixtures.Read("test-form-2026-11-09.html"))).Form!;

    private static PersonSubmission Person(RegistrationForm f, string name = "James Test", string? loan = null, int sessions = 1) =>
        new(Guid.NewGuid(), name, "Wicklow Archery", "TEST-1",
            f.Sessions.Take(sessions).ToList(), f.PaymentOptions[0], loan);

    [Fact]
    public void Build_IncludesEachEntryOnce()
    {
        var f = Form();
        var p = PayloadBuilder.Build(f, Person(f));
        Assert.Single(p, kv => kv.Key == "entry.353097525" && kv.Value == "James Test");
        Assert.Single(p, kv => kv.Key == "entry.859175428" && kv.Value == "Wicklow Archery");
        Assert.Single(p, kv => kv.Key == "entry.168390220" && kv.Value == "TEST-1");
        Assert.Single(p, kv => kv.Key == "entry.1751963076" && kv.Value == f.PaymentOptions[0]);
    }

    [Fact]
    public void Build_OneVenuePairPerSession()
    {
        var f = Form();
        var p = PayloadBuilder.Build(f, Person(f, sessions: 2));
        var venue = p.Where(kv => kv.Key == "entry.2011193388").Select(kv => kv.Value).ToList();
        Assert.Equal(f.Sessions.Take(2).ToList(), venue);
    }

    [Fact]
    public void Build_TrimsNameClubAndNumber()
    {
        var f = Form();
        var s = new PersonSubmission(Guid.NewGuid(), "  James  ", "  Club ", " 12 ", new[] { f.Sessions[0] }, f.PaymentOptions[0], null);
        var p = PayloadBuilder.Build(f, s);
        Assert.Contains(new("entry.353097525", "James"), p);
        Assert.Contains(new("entry.859175428", "Club"), p);
        Assert.Contains(new("entry.168390220", "12"), p);
    }

    [Fact]
    public void Build_NoLoan_OmitsLoanValue()
    {
        var f = Form();
        var p = PayloadBuilder.Build(f, Person(f));
        Assert.DoesNotContain(p, kv => kv.Key == "entry.1412580528" && kv.Value != "");
    }

    [Fact]
    public void Build_WithLoan_IncludesLoan()
    {
        var f = Form();
        var p = PayloadBuilder.Build(f, Person(f, loan: f.LoanOptions[0]));
        Assert.Single(p, kv => kv.Key == "entry.1412580528" && kv.Value == f.LoanOptions[0]);
    }

    [Fact]
    public void Build_IncludesHiddenFieldsAndSentinels()
    {
        var f = Form();
        var p = PayloadBuilder.Build(f, Person(f));
        Assert.Contains(new("fbzx", "-1521781842944014169"), p);
        Assert.Contains(new("entry.2011193388_sentinel", ""), p);
        Assert.Contains(new("entry.1751963076_sentinel", ""), p);
    }

    [Fact]
    public void Build_OrdersHiddenFirst_AndDoesNotMutateForm()
    {
        var f = Form();
        var before = f.HiddenFields.Count;
        var p = PayloadBuilder.Build(f, Person(f));
        Assert.Equal(f.HiddenFields.ToList(), p.Take(before).ToList());
        Assert.Equal(before, f.HiddenFields.Count);
    }
}
