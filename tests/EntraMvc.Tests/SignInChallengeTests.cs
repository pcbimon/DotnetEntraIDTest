using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;

namespace EntraMvc.Tests;

// Requires the entra-local emulator: `docker compose up -d` (see README).
public class SignInChallengeTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TenantId = "11111111-1111-1111-1111-111111111111";
    private const string ClientId = "c1c68d28-0643-42a1-9bcf-55a4ec06eaf5";

    [Fact]
    public async Task Anonymous_request_redirects_to_entra_authorize_with_configured_client()
    {
        // Arrange: pin emulator config so the test ignores whatever is in local user-secrets.
        var client = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAd:Instance"] = "https://localhost:8443/",
                ["AzureAd:TenantId"] = TenantId,
                ["AzureAd:ClientId"] = ClientId,
                ["AzureAd:CallbackPath"] = "/signin-oidc",
                ["AzureAd:ClientSecret"] = "not-used-before-redeem",
            }))).CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        // Act
        var response = await client.GetAsync("/");

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.Equal($"https://localhost:8443/{TenantId}/oauth2/v2.0/authorize", location.GetLeftPart(UriPartial.Path));

        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal(ClientId, query["client_id"]);
        Assert.Equal("https://localhost/signin-oidc", query["redirect_uri"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Contains("openid", query["scope"].ToString());
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.False(string.IsNullOrEmpty(query["state"]));
        Assert.False(string.IsNullOrEmpty(query["nonce"]));
    }
}
