using LivestreamRecorderBackend.Interfaces;
using Microsoft.AspNetCore.Authentication;
using OAuth2.Client.Impl;
using OAuth2.Infrastructure;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LivestreamRecorderBackend.Services.Authentication;

public class GithubService : IAuthenticationCodeHandlerService, IAuthenticationHandlerService
{
    private readonly HttpClient _httpClient;

    public string ClientId { get; } = Environment.GetEnvironmentVariable("GITHUB_PROVIDER_AUTHENTICATION_ID")!;
    public string ClientSecret { get; } = Environment.GetEnvironmentVariable("GITHUB_PROVIDER_AUTHENTICATION_SECRET")!;

    public GithubService(
        IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("client");

        // Note: Github requires this header (and User-Agent header) to be set.
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
    }

    /// <summary>
    /// Request access token with authorization code.
    /// </summary>
    /// <param name="authorization_code"></param>
    /// <returns></returns>
    public async Task<string> GetIdTokenAsync(string authorization_code, string redirectUri)
    {
        var githubClient = new GitHubClient(new RequestFactory(),
             new OAuth2.Configuration.ClientConfiguration
             {
                 ClientId = ClientId,
                 ClientSecret = ClientSecret,
                 RedirectUri = redirectUri,
                 Scope = "user:email"
             });

        var token = await githubClient.GetTokenAsync(new() { { "code", authorization_code } });

        return token;
    }

    public async Task<ClaimsPrincipal> GetUserInfoFromTokenAsync(string token)
    {
        _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
        var response = await _httpClient.GetAsync("https://api.github.com/user");
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"An error occurred when retrieving Github user information ({response.StatusCode}). Please check if the authentication information is correct.");
        }

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var identity = new ClaimsIdentity("github");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, payload.RootElement.GetString("id") ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim(ClaimTypes.Name, payload.RootElement.GetString("login") ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim(ClaimTypes.GivenName, payload.RootElement.GetString("name")?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim(ClaimTypes.Surname, payload.RootElement.GetString("name")?.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim(ClaimTypes.Email, payload.RootElement.GetString("email") ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim("picture", payload.RootElement.GetString("avatar_url") ?? "", ClaimValueTypes.String));

        return new ClaimsPrincipal(identity);
    }
}
