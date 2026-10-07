using System.Net;
using System.Net.Http.Json;

namespace PetAdoption.Api.Tests;

public class AdoptionTests
{
    private static Task<List<RequestView>> Requests(HttpClient admin) =>
        admin.GetFromJsonAsync<List<RequestView>>("/api/admin/adoption-requests")!;

    [Fact]
    public async Task Request_ForMultiplePets_CreatesOneRequestEach_WithPetInfo()
    {
        using var f = new ApiFactory();
        var admin = await f.AdminClient();
        var a = await Seed.ApprovedPet(admin, "Kedi", "Pamuk");
        var b = await Seed.ApprovedPet(admin, "Köpek", "Karabaş");

        var res = await Seed.Request(admin, "Ali Veli", a, b);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var requests = await Requests(admin);
        Assert.Equal(2, requests.Count);
        Assert.Contains(requests, r => r.PetId == a && r.PetName == "Pamuk" && r.FullName == "Ali Veli");
        Assert.Contains(requests, r => r.PetId == b && r.PetName == "Karabaş" && r.PetType == "Köpek");
    }

    [Fact]
    public async Task Request_DuplicatePetIds_AreCollapsed()
    {
        using var f = new ApiFactory();
        var admin = await f.AdminClient();
        var a = await Seed.ApprovedPet(admin);

        var res = await Seed.Request(admin, "Ali Veli", a, a);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Single(await Requests(admin));
    }

    [Fact]
    public async Task Request_EmptyPetIds_Returns400()
    {
        using var f = new ApiFactory();
        var res = await Seed.Request(f.PublicClient(), "Ali Veli");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Request_MissingPetIds_Returns400()
    {
        using var f = new ApiFactory();
        var res = await f.PublicClient().PostAsJsonAsync("/api/adoption-requests",
            new { fullName = "Ali", phone = "1", city = "Ankara" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Theory]
    [InlineData("  ", "1", "Ankara")]
    [InlineData("Ali", "", "Ankara")]
    [InlineData("Ali", "1", "\t")]
    public async Task Request_BlankFields_Return400(string fullName, string phone, string city)
    {
        using var f = new ApiFactory();
        var admin = await f.AdminClient();
        var a = await Seed.ApprovedPet(admin);

        var res = await admin.PostAsJsonAsync("/api/adoption-requests", new { fullName, phone, city, petIds = new[] { a } });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Request_PendingOrUnknownPet_Returns400_AndCreatesNothing()
    {
        using var f = new ApiFactory();
        var admin = await f.AdminClient();
        var approved = await Seed.ApprovedPet(admin);
        var pending = await Seed.PendingPet(admin, "Kuş", "Cik");

        var withPending = await Seed.Request(admin, "Ali Veli", approved, pending);
        var withUnknown = await Seed.Request(admin, "Ali Veli", approved, 9999);

        Assert.Equal(HttpStatusCode.BadRequest, withPending.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, withUnknown.StatusCode);
        Assert.Empty(await Requests(admin));
    }

    [Fact]
    public async Task Accept_DeletesPetAndAllItsRequests_ButKeepsOthers()
    {
        using var f = new ApiFactory();
        var admin = await f.AdminClient();
        var petA = await Seed.ApprovedPet(admin, "Kedi", "Pamuk");
        var petB = await Seed.ApprovedPet(admin, "Köpek", "Karabaş");
        await Seed.Request(admin, "Ali", petA);
        await Seed.Request(admin, "Veli", petA);
        await Seed.Request(admin, "Can", petB);
        var aliRequest = (await Requests(admin)).Single(r => r.FullName == "Ali");

        var res = await admin.PostAsync($"/api/admin/adoption-requests/{aliRequest.Id}/accept", null);

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        var remaining = await Requests(admin);
        Assert.Equal("Can", Assert.Single(remaining).FullName);
        var approved = await admin.GetFromJsonAsync<List<PublicPetView>>("/api/pets/approved");
        Assert.Equal(petB, Assert.Single(approved!).Id);
    }

    [Fact]
    public async Task Accept_Twice_SecondReturns404()
    {
        using var f = new ApiFactory();
        var admin = await f.AdminClient();
        var pet = await Seed.ApprovedPet(admin);
        await Seed.Request(admin, "Ali", pet);
        var id = (await Requests(admin)).Single().Id;

        var first = await admin.PostAsync($"/api/admin/adoption-requests/{id}/accept", null);
        var second = await admin.PostAsync($"/api/admin/adoption-requests/{id}/accept", null);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    [Fact]
    public async Task RejectRequest_RemovesRequest_KeepsPet_AndUnknownReturns404()
    {
        using var f = new ApiFactory();
        var admin = await f.AdminClient();
        var pet = await Seed.ApprovedPet(admin);
        await Seed.Request(admin, "Ali", pet);
        var id = (await Requests(admin)).Single().Id;

        var res = await admin.DeleteAsync($"/api/admin/adoption-requests/{id}");
        var unknown = await admin.DeleteAsync("/api/admin/adoption-requests/9999");

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Empty(await Requests(admin));
        Assert.Single((await admin.GetFromJsonAsync<List<PublicPetView>>("/api/pets/approved"))!);
    }

    [Theory]
    [InlineData("GET", "/api/admin/adoption-requests")]
    [InlineData("POST", "/api/admin/adoption-requests/1/accept")]
    [InlineData("DELETE", "/api/admin/adoption-requests/1")]
    [InlineData("GET", "/api/admin/stats")]
    public async Task AdminEndpoints_WithoutAdminToken_Return401(string method, string url)
    {
        using var f = new ApiFactory();
        var res = await f.PublicClient().SendAsync(new HttpRequestMessage(new HttpMethod(method), url));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Stats_CountsOnlyApprovedPets_ByType_SortedByCountThenType()
    {
        using var f = new ApiFactory();
        var admin = await f.AdminClient();
        await Seed.ApprovedPet(admin, "Köpek", "A");
        await Seed.ApprovedPet(admin, "Kedi", "B");
        await Seed.ApprovedPet(admin, "Kedi", "C");
        await Seed.PendingPet(admin, "Kuş", "D");

        var stats = await admin.GetFromJsonAsync<List<StatView>>("/api/admin/stats");

        Assert.Equal(new[] { ("Kedi", 2), ("Köpek", 1) }, stats!.Select(s => (s.PetType, s.Count)).ToArray());
    }
}
