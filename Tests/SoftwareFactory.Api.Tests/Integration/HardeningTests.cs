using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace SoftwareFactory.Api.Tests.Integration;

/// <summary>estandar-auth.md §3 and §6: rate limiting on /api/auth, CORS restricted to the frontend origin, security headers.</summary>
[Collection(ApiCollectionDefinition.Name)]
public sealed class HardeningTests(ApiFixture fixture)
{
    [Fact]
    public async Task Auth_endpoints_are_throttled_per_client_with_a_problem_details_429()
    {
        using var throttled = fixture.CreateThrottledFactory(permitLimit: 3);
        using var client = ApiFixture.CreateClient(throttled);

        for (var i = 0; i < 3; i++)
        {
            using var allowed = await client.PostLoginAsync("nobody@local", "whatever-password-2026");
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        using var rejected = await client.PostLoginAsync("nobody@local", "whatever-password-2026");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(rejected.Headers.RetryAfter);
        var problem = await rejected.Content.ReadFromJsonAsync<ProblemDetails>(AuthClientExtensions.Json);
        Assert.Equal("Demasiadas solicitudes. Inténtelo de nuevo más tarde.", problem!.Detail);

        using var healthStillFine = await client.GetAsync(new Uri("/health", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, healthStillFine.StatusCode);
    }

    [Fact]
    public async Task Responses_carry_the_standard_security_headers()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cors_allows_the_frontend_origin_with_credentials_and_rejects_others()
    {
        using var client = fixture.CreateClient();

        using var allowed = await Preflight(client, ApiFixture.AllowedOrigin);
        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        Assert.Equal(ApiFixture.AllowedOrigin, allowed.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", allowed.Headers.GetValues("Access-Control-Allow-Credentials").Single());

        using var rejected = await Preflight(client, "https://evil.example");
        Assert.False(rejected.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static async Task<HttpResponseMessage> Preflight(HttpClient client, string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, new Uri("/api/auth/login", UriKind.Relative));
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");
        return await client.SendAsync(request);
    }
}
