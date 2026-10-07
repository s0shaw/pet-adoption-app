using System.Security.Cryptography;
using System.Text;

namespace PetAdoption.Api;

public class AdminSessions
{
    private readonly HashSet<string> tokens = new();
    private readonly object gate = new();

    public string Create()
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        lock (gate) tokens.Add(token);
        return token;
    }

    public bool IsValid(string? token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        lock (gate) return tokens.Contains(token);
    }
}

public class AdminOptions
{
    public string? Username { get; set; }
    public string? Password { get; set; }

    // Non-short-circuit `&` keeps timing independent of which field is wrong.
    public bool Matches(string? username, string? password) =>
        Security.Same(username, Username) & Security.Same(password, Password);
}

public class AdminOnlyFilter(AdminSessions sessions) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var token = ctx.HttpContext.Request.Headers["X-Admin-Token"].FirstOrDefault();
        return sessions.IsValid(token) ? await next(ctx) : ApiResults.Unauthorized("Yetkisiz.");
    }
}

public static class Security
{
    public static bool Same(string? a, string? b) =>
        a is not null && b is not null &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    public static void UseAppToken(this WebApplication app)
    {
        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                var expected = app.Configuration["AppToken"];
                var given = ctx.Request.Headers["X-App-Token"].FirstOrDefault();
                if (string.IsNullOrEmpty(expected) || !Same(given, expected))
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await ctx.Response.WriteAsJsonAsync(new { error = "Yetkisiz." });
                    return;
                }
            }
            await next();
        });
    }
}
