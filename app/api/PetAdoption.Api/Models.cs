namespace PetAdoption.Api;

public enum PetStatus { Pending, Approved }

public class Pet
{
    public int Id { get; set; }
    public string OwnerName { get; set; } = "";
    public string City { get; set; } = "";
    public string Phone { get; set; } = "";
    public string PetName { get; set; } = "";
    public string PetType { get; set; } = "";
    public string Breed { get; set; } = "";
    public int Age { get; set; }
    public string Neutered { get; set; } = "";
    public PetStatus Status { get; set; } = PetStatus.Pending;
    public List<AdoptionRequest> Requests { get; set; } = new();
}

public class AdoptionRequest
{
    public int Id { get; set; }
    public int PetId { get; set; }
    public Pet? Pet { get; set; }
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string City { get; set; } = "";
}

public record PetInput(string? OwnerName, string? City, string? Phone, string? PetName, string? PetType, string? Breed, int? Age, string? Neutered)
{
    public string? Validate()
    {
        if (Input.AnyBlank(OwnerName, City, Phone, PetName, PetType, Breed, Neutered))
            return Input.Incomplete;
        return Age is null or < 0 ? "Yaş alanına geçerli bir sayı giriniz." : null;
    }

    public Pet ToPet() => new()
    {
        OwnerName = OwnerName!.Trim(),
        City = City!.Trim(),
        Phone = Phone!.Trim(),
        PetName = PetName!.Trim(),
        PetType = PetType!.Trim(),
        Breed = Breed!.Trim(),
        Age = Age!.Value,
        Neutered = Neutered!.Trim()
    };
}

public record AdoptionInput(string? FullName, string? Phone, string? City, List<int>? PetIds)
{
    public string? Validate() => Input.AnyBlank(FullName, Phone, City) ? Input.Incomplete : null;

    public List<int> DistinctPetIds() => (PetIds ?? new()).Distinct().ToList();

    public AdoptionRequest ToRequest(int petId) => new()
    {
        PetId = petId,
        FullName = FullName!.Trim(),
        Phone = Phone!.Trim(),
        City = City!.Trim()
    };
}

public record LoginInput(string? Username, string? Password);

public record PetDto(int Id, string OwnerName, string City, string Phone, string PetName, string PetType, string Breed, int Age, string Neutered, string Status)
{
    public static PetDto From(Pet p) =>
        new(p.Id, p.OwnerName, p.City, p.Phone, p.PetName, p.PetType, p.Breed, p.Age, p.Neutered, p.Status.ToString());
}

public record PublicPetDto(int Id, string OwnerName, string City, string PetName, string PetType, string Breed);
public record RequestDto(int Id, int PetId, string FullName, string Phone, string City, string PetName, string PetType, string Breed);
public record StatDto(string PetType, int Count);

static class Input
{
    public const string Incomplete = "Lütfen tüm bilgileri eksiksiz doldurunuz.";

    public static bool AnyBlank(params string?[] values) => values.Any(string.IsNullOrWhiteSpace);
}
