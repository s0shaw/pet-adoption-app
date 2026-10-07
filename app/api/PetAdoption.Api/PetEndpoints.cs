using Microsoft.EntityFrameworkCore;

namespace PetAdoption.Api;

public static class PetEndpoints
{
    public static void MapPetEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/pets", async (PetInput input, AppDb db) =>
        {
            if (input.Validate() is { } error) return ApiResults.Bad(error);

            var pet = input.ToPet();
            db.Pets.Add(pet);
            await db.SaveChangesAsync();
            return Results.Created($"/api/pets/{pet.Id}", PetDto.From(pet));
        });

        api.MapGet("/pets/approved", async (AppDb db) =>
        {
            var pets = await db.Pets.Where(p => p.Status == PetStatus.Approved).OrderBy(p => p.Id).ToListAsync();
            return pets.Select(p => new PublicPetDto(p.Id, p.OwnerName, p.City, p.PetName, p.PetType, p.Breed));
        });
    }
}
