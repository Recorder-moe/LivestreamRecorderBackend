using System;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using LivestreamRecorderBackend.Interfaces;
using OAuth2.Client.Impl;
using OAuth2.Configuration;
using OAuth2.Infrastructure;

namespace LivestreamRecorderBackend.Services.Authentication;

public class GitHubService(IHttpClientFactory httpClientFactory) : IAuthenticationCodeHandlerService, IAuthenticationHandlerService
{
    public string ClientId { get; } = Environment.GetEnvironmentVariable("GITHUB_PROVIDER_AUTHENTICATION_ID")!;
    public string ClientSecret { get; } = Environment.GetEnvironmentVariable("GITHUB_PROVIDER_AUTHENTICATION_SECRET")!;

    /// <summary>
    ///     Request access token with authorization code.
    /// </summary>
    /// <param name="authorizationCode"></param>
    /// <param name="redirectUri"></param>
    /// <returns></returns>
    public async Task<string> GetIdTokenAsync(string authorizationCode, string redirectUri)
    {
        var githubClient = new GitHubClient(new RequestFactory(),
                                            new ClientConfiguration
                                            {
                                                ClientId = ClientId,
                                                ClientSecret = ClientSecret,
                                                RedirectUri = redirectUri,
                                                Scope = "user:email"
                                            });

        string? token = await githubClient.GetTokenAsync(new NameValueCollection
                                                             { { "code", authorizationCode } });

        return token;
    }

    public async Task<ClaimsPrincipal> GetUserInfoFromTokenAsync(string token)
    {
        HttpClient httpClient = httpClientFactory.CreateClient("client");

        // Note: GitHub requires this header (and User-Agent header) to be set.
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
        httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
        HttpResponseMessage response = await httpClient.GetAsync("https://api.github.com/user");
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"An error occurred when retrieving GitHub user information ({response.StatusCode}). Please check if the authentication information is correct.");

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var identity = new ClaimsIdentity("github");
        identity.AddClaim(
            new Claim(ClaimTypes.NameIdentifier, payload.RootElement.GetProperty("id").GetInt32().ToString(), ClaimValueTypes.Integer32));

        identity.AddClaim(new Claim(ClaimTypes.Name, payload.RootElement.GetProperty("login").GetString() ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim(ClaimTypes.GivenName,
                                    payload.RootElement.GetProperty("name").GetString()?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                           .FirstOrDefault() ?? "",
                                    ClaimValueTypes.String));

        identity.AddClaim(new Claim(ClaimTypes.Surname,
                                    payload.RootElement.GetProperty("name").GetString()?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                           .LastOrDefault() ?? "",
                                    ClaimValueTypes.String));

        identity.AddClaim(new Claim(ClaimTypes.Email, payload.RootElement.GetProperty("email").GetString() ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim("picture", payload.RootElement.GetProperty("avatar_url").GetString() ?? "", ClaimValueTypes.String));

        return new ClaimsPrincipal(identity);
    }
}
