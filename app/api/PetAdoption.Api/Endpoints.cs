namespace PetAdoption.Api;

public static class Endpoints
{
    public static void MapApi(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        var api = app.MapGroup("/api");
        api.MapPetEndpoints();
        api.MapAdoptionEndpoints();
        api.MapAdminEndpoints();
    }
}
