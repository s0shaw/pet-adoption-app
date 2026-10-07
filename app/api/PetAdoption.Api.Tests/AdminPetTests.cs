using System.Net;
using System.Net.Http.Json;

namespace PetAdoption.Api.Tests;

public class AdminPetTests
{
    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        using var f = new ApiFactory();
        var res = await f.PublicClient().PostAsJsonAsync("/api/login", new { username = "admin", password = "nope" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Login_MissingBody_Fields_Returns401()
    {
        using var f = new ApiFactory();
        var res = await f.PublicClient().PostAsJsonAsync("/api/login", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/admin/pets/pending")]
    [InlineData("POST", "/api/admin/pets/1/approve")]
    [InlineData("DELETE", "/api/admin/pets/1")]
    public async Task AdminEndpoints_WithoutAdminToken_Return401(string method, string url)
    {
        using var f = new ApiFactory();
        var res = await f.PublicClient().SendAsync(new HttpRequestMessage(new HttpMethod(method), url));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Pending_ListsNewPets_WithSpecialCharactersIntact()
    {
        using var f = new ApiFactory();
        var admin = await f.AdminClient();
        var weird = "<b>x</b> & \"q\" İıŞşĞğ " + new string('y', 500);
        var res = await admin.PostAsJsonAsync("/api/pets", new
        {
            ownerName = weird, city = "İzmir", phone = "1", petName = weird,
            petType = "Kedi", breed = "T", age = 1, neutered = "Evet"
        });
        res.EnsureSuccessStatusCode();

        var pending = await admin.GetFromJsonAsync<List<PetView>>("/api/admin/pets/pending");

        var pet = Assert.Single(pending!);
        Assert.Equal(weird, pet.OwnerName);
        Assert.Equal(weird, pet.PetName);
    }

    [Fact]
    public async Task Approve_MovesPetFromPendingToApprovedList_AndIsIdempotent()
    {
        using var f = new ApiFactory();
        var admin = await f.AdminClient();
        var id = await Seed.PendingPet(admin);

        var first = await admin.PostAsync($"/api/admin/pets/{id}/approve", null);
        var second = await admin.PostAsync($"/api/admin/pets/{id}/approve", null);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Empty((await admin.GetFromJsonAsync<List<PetView>>("/api/admin/pets/pending"))!);
        var approved = await admin.GetFromJsonAsync<List<PublicPetView>>("/api/pets/approved");
        Assert.Equal(id, Assert.Single(approved!).Id);
    }

    [Fact]
    public async Task ApprovedList_DoesNotLeakPhoneNumber()
    {
        using var f = new ApiFactory();
        var admin = await f.AdminClient();
        await Seed.ApprovedPet(admin);

        var body = await admin.GetStringAsync("/api/pets/approved");

        Assert.DoesNotContain("5551112233", body);
        Assert.DoesNotContain("phone", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reject_DeletesPet_AndSecondDeleteReturns404()
    {
        using var f = new ApiFactory();
        var admin = await f.AdminClient();
        var id = await Seed.PendingPet(admin);

        var first = await admin.DeleteAsync($"/api/admin/pets/{id}");
        var second = await admin.DeleteAsync($"/api/admin/pets/{id}");

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
        Assert.Empty((await admin.GetFromJsonAsync<List<PetView>>("/api/admin/pets/pending"))!);
    }

    [Fact]
    public async Task Approve_UnknownId_Returns404()
    {
        using var f = new ApiFactory();
        var admin = await f.AdminClient();
        var res = await admin.PostAsync("/api/admin/pets/9999/approve", null);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
