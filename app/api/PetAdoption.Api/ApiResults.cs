namespace PetAdoption.Api;

public static class ApiResults
{
    public static IResult Bad(string message) => Results.BadRequest(new { error = message });

    public static IResult Missing() => Results.NotFound(new { error = "Kayıt bulunamadı." });

    public static IResult Unauthorized(string message) =>
        Results.Json(new { error = message }, statusCode: StatusCodes.Status401Unauthorized);
}
