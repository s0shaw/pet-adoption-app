using Microsoft.EntityFrameworkCore;

namespace PetAdoption.Api;

public static class AdoptionEndpoints
{
    public static void MapAdoptionEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/adoption-requests", async (AdoptionInput input, AppDb db) =>
        {
            if (input.Validate() is { } error) return ApiResults.Bad(error);

            var ids = input.DistinctPetIds();
            if (ids.Count == 0)
                return ApiResults.Bad("Lütfen bir hayvan seçiniz.");

            var openCount = await db.Pets.CountAsync(p => ids.Contains(p.Id) && p.Status == PetStatus.Approved);
            if (openCount != ids.Count)
                return ApiResults.Bad("Seçilen hayvanlardan biri artık sahiplenmeye açık değil.");

            db.AdoptionRequests.AddRange(ids.Select(input.ToRequest));
            await db.SaveChangesAsync();
            return Results.Created("/api/adoption-requests", new { count = ids.Count });
        });
    }
}
