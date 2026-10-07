using System.Net;
using System.Net.Http.Json;

namespace PetAdoption.Api.Tests;

public class PetTests
{
    public static IEnumerable<object[]> InvalidPets()
    {
        yield return new object[] { new { ownerName = "   ", city = "İzmir", phone = "1", petName = "P", petType = "Kedi", breed = "T", age = 3, neutered = "Evet" } };
        yield return new object[] { new { ownerName = "A", city = "\t ", phone = "1", petName = "P", petType = "Kedi", breed = "T", age = 3, neutered = "Evet" } };
        yield return new object[] { new { ownerName = "A", city = "İzmir", petName = "P", petType = "Kedi", breed = "T", age = 3, neutered = "Evet" } };
        yield return new object[] { new { ownerName = "A", city = "İzmir", phone = "1", petName = "", petType = "Kedi", breed = "T", age = 3, neutered = "Evet" } };
        yield return new object[] { new { ownerName = "A", city = "İzmir", phone = "1", petName = "P", petType = "Kedi", breed = "T", age = 3, neutered = " " } };
        yield return new object[] { new { ownerName = "A", city = "İzmir", phone = "1", petName = "P", petType = "Kedi", breed = "T", age = -1, neutered = "Evet" } };
        yield return new object[] { new { ownerName = "A", city = "İzmir", phone = "1", petName = "P", petType = "Kedi", breed = "T", neutered = "Evet" } };
        yield return new object[] { new { ownerName = "A", city = "İzmir", phone = "1", petName = "P", petType = "Kedi", breed = "T", age = "abc", neutered = "Evet" } };
    }

    [Fact]
    public async Task CreatePet_Valid_Returns201_Pending_AndTrims()
    {
        using var f = new ApiFactory();
        var client = f.PublicClient();

        var res = await client.PostAsJsonAsync("/api/pets", new
        {
            ownerName = "  Ayşe Yılmaz ", city = "İzmir", phone = "5551112233",
            petName = " Pamuk ", petType = "Kedi", breed = "Tekir", age = 3, neutered = "Evet"
        });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var pet = (await res.Content.ReadFromJsonAsync<PetView>())!;
        Assert.Equal("Pending", pet.Status);
        Assert.Equal("Pamuk", pet.PetName);
        Assert.Equal("Ayşe Yılmaz", pet.OwnerName);
    }

    [Theory]
    [MemberData(nameof(InvalidPets))]
    public async Task CreatePet_Invalid_Returns400(object payload)
    {
        using var f = new ApiFactory();
        var res = await f.PublicClient().PostAsJsonAsync("/api/pets", payload);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task CreatePet_SpecialCharacters_RoundTripUnchanged()
    {
        using var f = new ApiFactory();
        var weird = "<script>alert(1)</script> & \"q\" 'a' İıŞşĞğÜüÖöÇç " + new string('x', 500);

        var res = await f.PublicClient().PostAsJsonAsync("/api/pets", new
        {
            ownerName = weird, city = "İzmir", phone = "1", petName = weird,
            petType = "Kedi", breed = "T", age = 1, neutered = "Evet"
        });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var pet = (await res.Content.ReadFromJsonAsync<PetView>())!;
        Assert.Equal(weird, pet.OwnerName);
        Assert.Equal(weird, pet.PetName);
    }

    [Fact]
    public async Task ApprovedList_DoesNotContainPendingPets()
    {
        using var f = new ApiFactory();
        var client = f.PublicClient();
        await Seed.PendingPet(client);

        var list = await client.GetFromJsonAsync<List<PublicPetView>>("/api/pets/approved");

        Assert.Empty(list!);
    }
}
