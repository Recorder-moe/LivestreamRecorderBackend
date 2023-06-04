using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.AspNetCore.Authentication;
using System;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LivestreamRecorderBackend.Services.Authentication;

public class GoogleService
{
    private readonly HttpClient _httpClient;

    private static string ClientId { get; } = Environment.GetEnvironmentVariable("GOOGLE_PROVIDER_AUTHENTICATION_ID")!;
    private static string ClientSecret { get; } = Environment.GetEnvironmentVariable("GOOGLE_PROVIDER_AUTHENTICATION_SECRET")!;

    public GoogleService(
        IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }

    /// <summary>
    /// Request access token with authorization code.
    /// </summary>
    /// <param name="authorization_code"></param>
    /// <returns></returns>
    public async Task<string> GetIdTokenAsync(string authorization_code, string redirectUri)
    {
        AuthorizationCodeTokenRequest request = new()
        {
            Code = authorization_code,
            RedirectUri = redirectUri,
            ClientId = ClientId,
            ClientSecret = ClientSecret,
            Scope = "openid profile email"
        };

        TokenResponse responce = await request.ExecuteAsync(_httpClient,
                                                            GoogleAuthConsts.OidcTokenUrl,
                                                            new(),
                                                            Google.Apis.Util.SystemClock.Default);

        return responce.IdToken;
    }

    public async Task<ClaimsPrincipal> GetUserInfoFromTokenAsync(string token)
    {
        _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
        var response = await _httpClient.GetAsync("https://www.googleapis.com/oauth2/v3/userinfo");
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"An error occurred when retrieving Google user information ({response.StatusCode}). Please check if the authentication information is correct.");
        }

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var identity = new ClaimsIdentity("google");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, payload.RootElement.GetString("sub") ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim(ClaimTypes.Name, payload.RootElement.GetString("name") ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim(ClaimTypes.GivenName, payload.RootElement.GetString("given_name") ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim(ClaimTypes.Surname, payload.RootElement.GetString("family_name") ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim(ClaimTypes.Email, payload.RootElement.GetString("email") ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim("picture", payload.RootElement.GetString("picture") ?? "", ClaimValueTypes.String));

        return new ClaimsPrincipal(identity);
    }
}
