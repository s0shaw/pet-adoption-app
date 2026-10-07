using System.Net;

namespace PetAdoption.Api.Tests;

public class SecurityTests
{
    [Fact]
    public async Task Health_IsOpenWithoutToken()
    {
        using var f = new ApiFactory();
        var res = await f.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Api_WithoutAppToken_Returns401()
    {
        using var f = new ApiFactory();
        var res = await f.CreateClient().GetAsync("/api/pets/approved");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Api_WithWrongAppToken_Returns401()
    {
        using var f = new ApiFactory();
        var client = f.CreateClient();
        client.DefaultRequestHeaders.Add("X-App-Token", "wrong");
        var res = await client.GetAsync("/api/pets/approved");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Preflight_IsAllowedWithoutToken()
    {
        using var f = new ApiFactory();
        var req = new HttpRequestMessage(HttpMethod.Options, "/api/pets");
        req.Headers.Add("Origin", "null");
        req.Headers.Add("Access-Control-Request-Method", "POST");
        req.Headers.Add("Access-Control-Request-Headers", "x-app-token,content-type");

        var res = await f.CreateClient().SendAsync(req);

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.True(res.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
