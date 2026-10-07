using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace PetAdoption.Api.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    public const string AppToken = "test-app-token";
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"petadoption-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("DbPath", dbPath);
        builder.UseSetting("AppToken", AppToken);
        builder.UseSetting("Admin:Username", "admin");
        builder.UseSetting("Admin:Password", "admin");
    }

    public HttpClient PublicClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-App-Token", AppToken);
        return client;
    }

    public async Task<HttpClient> AdminClient()
    {
        var client = PublicClient();
        var res = await client.PostAsJsonAsync("/api/login", new { username = "admin", password = "admin" });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<LoginResult>();
        client.DefaultRequestHeaders.Add("X-Admin-Token", body!.Token);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        SqliteConnection.ClearAllPools();
        try { File.Delete(dbPath); } catch (IOException) { }
    }
}

public record LoginResult(string Token);
public record PetView(int Id, string OwnerName, string City, string Phone, string PetName, string PetType, string Breed, int Age, string Neutered, string Status);
public record PublicPetView(int Id, string OwnerName, string City, string PetName, string PetType, string Breed);
public record RequestView(int Id, int PetId, string FullName, string Phone, string City, string PetName, string PetType, string Breed);
public record StatView(string PetType, int Count);

public static class Seed
{
    public static object Pet(string type = "Kedi", string name = "Pamuk") => new
    {
        ownerName = "Ayşe Yılmaz",
        city = "İzmir",
        phone = "5551112233",
        petName = name,
        petType = type,
        breed = "Tekir",
        age = 3,
        neutered = "Evet"
    };

    public static async Task<int> PendingPet(HttpClient client, string type = "Kedi", string name = "Pamuk")
    {
        var res = await client.PostAsJsonAsync("/api/pets", Pet(type, name));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<PetView>())!.Id;
    }

    public static async Task<int> ApprovedPet(HttpClient admin, string type = "Kedi", string name = "Pamuk")
    {
        var id = await PendingPet(admin, type, name);
        (await admin.PostAsync($"/api/admin/pets/{id}/approve", null)).EnsureSuccessStatusCode();
        return id;
    }

    public static async Task<HttpResponseMessage> Request(HttpClient client, string fullName, params int[] petIds)
        => await client.PostAsJsonAsync("/api/adoption-requests", new { fullName, phone = "5559998877", city = "Ankara", petIds });
}
