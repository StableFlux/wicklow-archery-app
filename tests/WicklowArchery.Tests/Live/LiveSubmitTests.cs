using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WicklowArchery.Core.Data;
using WicklowArchery.Core.Net;
using WicklowArchery.Core.Registration;
using Xunit;

namespace WicklowArchery.Tests.Live;

public class LiveSubmitTests
{
    [Fact]
    [Trait("Category", "Live")]
    public async Task Submit_ToTestForm_Succeeds()
    {
        Assert.False(FormUrls.IsClubForm(FormUrls.TestForm));
        var client = new ClubFormGuardClient(new HttpFormClient(HttpFormClient.CreateHttpClient()));
        var service = new RegistrationService(client, TimeProvider.System);

        var load = await service.LoadFormAsync(FormUrls.TestForm, CancellationToken.None);
        Assert.Null(load.Error);
        Assert.True(load.Check!.IsUsable);
        var form = load.Check.Form!;

        var club = new Club { Name = "Wicklow Archery", IsDefault = true };
        var person = new Person
        {
            FullName = $"TEST live {DateTime.Now:yyyy-MM-dd HH:mm}", ClubId = club.Id, ClubNumber = "TEST-0000",
            PaymentMethod = form.PaymentOptions[0],
        };
        var items = ConfirmationBuilder.Build(form, [new Selection(person, [form.Sessions[0]])], [club], []);
        var results = await service.SubmitAsync(form, items, null, CancellationToken.None);

        Console.WriteLine("LIVE NAME: " + person.FullName);
        var r = Assert.Single(results);
        Assert.True(r.State == PersonResultState.Submitted, r.Error);
    }
}
