using System;
using System.Collections.Generic;
using System.Linq;
using WicklowArchery.Core.Data;
using WicklowArchery.Core.Forms;
using WicklowArchery.Core.Net;
using WicklowArchery.Core.Registration;
using Xunit;

namespace WicklowArchery.Tests.Core;

public class ConfirmationBuilderTests
{
    private static RegistrationForm Form() =>
        FormMapper.Map(FormParser.Parse(Fixtures.Read("club-form-2026-11-09.html"))).Form!;

    private static readonly Club Club = new() { Name = "Wicklow Archery", IsDefault = true };

    private static Person P(string name = "Ann", string payment = "WICKLOW ARCHERY CLUB MEMBER - FREE NO PAYMENT", string? loan = null) =>
        new() { FullName = name, ClubId = Club.Id, ClubNumber = "123", PaymentMethod = payment, LoanBow = loan };

    private static IReadOnlyList<ConfirmItem> Run(RegistrationForm f, IReadOnlyList<Selection> sel, IReadOnlyList<HistoryEntry>? h = null) =>
        ConfirmationBuilder.Build(f, sel, [Club], h ?? []);

    [Fact]
    public void Build_PaymentNoLongerOnForm_BlocksOnlyThatPerson()
    {
        var f = Form();
        var bad = P("Bad", "NON-MEMBER");
        var good = P("Good");
        var items = Run(f, [new(bad, [f.Sessions[0]]), new(good, [f.Sessions[0]])]);
        Assert.True(items[0].IsBlocked);
        Assert.Null(items[0].Submission);
        Assert.Equal("Payment method \"NON-MEMBER\" is no longer on the form. Edit this person to pick one.", items[0].BlockedReason);
        Assert.False(items[1].IsBlocked);
        Assert.NotNull(items[1].Submission);
    }

    [Fact]
    public void Build_LoanNoLongerOnForm_Blocks()
    {
        var f = Form();
        var item = Run(f, [new(P(loan: "Gone bow"), [f.Sessions[0]])]).Single();
        Assert.Equal("Loan bow \"Gone bow\" is no longer on the form. Edit this person to change it.", item.BlockedReason);
        var ok = Run(f, [new(P(loan: "Left handed adults bow"), [f.Sessions[0]])]).Single();
        Assert.False(ok.IsBlocked);
        Assert.Equal("Left handed adults bow", ok.Submission!.LoanBow);
    }

    [Fact]
    public void Build_SelectionWithNoSessions_Omitted()
    {
        var f = Form();
        var items = Run(f, [new(P("Empty"), []), new(P("Has"), [f.Sessions[0]])]);
        Assert.Equal("Has", Assert.Single(items).Person.FullName);
    }

    [Fact]
    public void Build_RepeatSession_Flagged()
    {
        var f = Form();
        var p = P();
        var h = new HistoryEntry { PersonId = p.Id, SessionText = f.Sessions[1], Succeeded = true };
        var failed = new HistoryEntry { PersonId = p.Id, SessionText = f.Sessions[0], Succeeded = false };
        var item = Run(f, [new(p, [f.Sessions[0], f.Sessions[1]])], [h, failed]).Single();
        Assert.Equal([f.Sessions[1]], item.RepeatSessions);
        Assert.False(item.IsBlocked);
    }

    [Fact]
    public void Build_ResolvesClubName()
    {
        var f = Form();
        var item = Run(f, [new(P(), [f.Sessions[0]])]).Single();
        Assert.Equal("Wicklow Archery", item.Submission!.ClubName);
    }

    [Fact]
    public void Build_MissingFieldsAndClub_BlockWithExactCopy()
    {
        var f = Form();
        var s = new[] { f.Sessions[0] };
        Assert.Equal("Missing full name.", Run(f, [new(P(" "), s)]).Single().BlockedReason);
        var noClub = P(); noClub.ClubId = Guid.NewGuid();
        Assert.Equal("No club set.", Run(f, [new(noClub, s)]).Single().BlockedReason);
        var noNum = P(); noNum.ClubNumber = "";
        Assert.Equal("Missing club number.", Run(f, [new(noNum, s)]).Single().BlockedReason);
        Assert.Equal("Payment method \"\" is no longer on the form. Edit this person to pick one.",
            Run(f, [new(P(payment: ""), s)]).Single().BlockedReason);
    }
    private static readonly string TestFormId = FormAddress.FormId(new Uri(FormUrls.TestForm))!;
    private static readonly string ClubFormId = FormAddress.FormId(new Uri(FormUrls.ClubForm))!;

    private static RegistrationForm TestForm() =>
        FormMapper.Map(FormParser.Parse(Fixtures.Read("test-form-2026-11-09.html"))).Form!;

    [Fact]
    public void History_FromTestForm_NotRegisteredOnClubForm()
    {
        var club = Form();
        var p = P();
        // Same person and session text, but sent to the test form.
        var sentToTest = new HistoryEntry { PersonId = p.Id, FormId = TestFormId, SessionText = club.Sessions[0], Succeeded = true };
        Assert.Empty(Run(club, [new(p, [club.Sessions[0]])], [sentToTest]).Single().RepeatSessions);

        var sentToClub = new HistoryEntry { PersonId = p.Id, FormId = ClubFormId, SessionText = club.Sessions[0], Succeeded = true };
        Assert.Equal([club.Sessions[0]], Run(club, [new(p, [club.Sessions[0]])], [sentToClub]).Single().RepeatSessions);
    }

    [Fact]
    public void History_NullFormId_TreatedAsClubForm()
    {
        var club = Form();
        var test = TestForm();
        var p = P(payment: test.PaymentOptions[0]);
        var onClub = new HistoryEntry { PersonId = p.Id, SessionText = club.Sessions[0], Succeeded = true };
        var onTest = new HistoryEntry { PersonId = p.Id, SessionText = test.Sessions[0], Succeeded = true };

        Assert.Equal([club.Sessions[0]], Run(club, [new(p, [club.Sessions[0]])], [onClub]).Single().RepeatSessions);
        Assert.Empty(Run(test, [new(p, [test.Sessions[0]])], [onTest]).Single().RepeatSessions);
    }
}
