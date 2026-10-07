using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace PetAdoption.Api;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/login", IResult (LoginInput input, IOptions<AdminOptions> admin, AdminSessions sessions) =>
            admin.Value.Matches(input.Username, input.Password)
                ? Results.Ok(new { token = sessions.Create() })
                : ApiResults.Unauthorized("Kullanıcı adı veya şifre hatalı."));

        var group = api.MapGroup("/admin").AddEndpointFilter<AdminOnlyFilter>();
        group.MapPetModeration();
        group.MapRequestModeration();
        group.MapStats();
    }

    private static void MapPetModeration(this RouteGroupBuilder admin)
    {
        admin.MapGet("/pets/pending", async (AppDb db) =>
        {
            var pets = await db.Pets.Where(p => p.Status == PetStatus.Pending).OrderBy(p => p.Id).ToListAsync();
            return pets.Select(PetDto.From);
        });

        admin.MapPost("/pets/{id:int}/approve", async (int id, AppDb db) =>
        {
            var pet = await db.Pets.FindAsync(id);
            if (pet is null) return ApiResults.Missing();
            pet.Status = PetStatus.Approved;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        admin.MapDelete("/pets/{id:int}", async (int id, AppDb db) =>
        {
            var pet = await db.Pets.FindAsync(id);
            if (pet is null) return ApiResults.Missing();
            db.Pets.Remove(pet);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static void MapRequestModeration(this RouteGroupBuilder admin)
    {
        admin.MapGet("/adoption-requests", async (AppDb db) =>
            await db.AdoptionRequests
                .OrderBy(r => r.Id)
                .Select(r => new RequestDto(r.Id, r.PetId, r.FullName, r.Phone, r.City,
                    r.Pet!.PetName, r.Pet.PetType, r.Pet.Breed))
                .ToListAsync());

        // Removing the pet cascades to every request for it.
        admin.MapPost("/adoption-requests/{id:int}/accept", async (int id, AppDb db) =>
        {
            var request = await db.AdoptionRequests.Include(r => r.Pet).FirstOrDefaultAsync(r => r.Id == id);
            if (request is null) return ApiResults.Missing();
            db.Pets.Remove(request.Pet!);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        admin.MapDelete("/adoption-requests/{id:int}", async (int id, AppDb db) =>
        {
            var request = await db.AdoptionRequests.FindAsync(id);
            if (request is null) return ApiResults.Missing();
            db.AdoptionRequests.Remove(request);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static void MapStats(this RouteGroupBuilder admin)
    {
        admin.MapGet("/stats", async (AppDb db) =>
        {
            var stats = await db.Pets
                .Where(p => p.Status == PetStatus.Approved)
                .GroupBy(p => p.PetType)
                .Select(g => new StatDto(g.Key, g.Count()))
                .ToListAsync();
            return stats.OrderByDescending(s => s.Count).ThenBy(s => s.PetType, StringComparer.CurrentCulture);
        });
    }
}
