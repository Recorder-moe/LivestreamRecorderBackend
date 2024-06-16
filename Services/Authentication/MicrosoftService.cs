using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using LivestreamRecorderBackend.Interfaces;

namespace LivestreamRecorderBackend.Services.Authentication;

public class MicrosoftService(IHttpClientFactory httpClientFactory) : IAuthenticationHandlerService
{
    public async Task<ClaimsPrincipal> GetUserInfoFromTokenAsync(string token)
    {
        HttpClient httpClient = httpClientFactory.CreateClient("client");
        httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
        HttpResponseMessage response = await httpClient.GetAsync("https://graph.microsoft.com/oidc/userinfo");
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"An error occurred when retrieving Microsoft user information ({response.StatusCode}). Please check if the authentication information is correct.");

        string json = await response.Content.ReadAsStringAsync();
        using var payload = JsonDocument.Parse(json);
        var identity = new ClaimsIdentity("aad");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, payload.RootElement.GetProperty("sub").GetString() ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim(ClaimTypes.Name,
                                    payload.RootElement.TryGetProperty("name", out JsonElement ele) ? ele.GetProperty("name").GetString() ?? "" : "",
                                    ClaimValueTypes.String));

        identity.AddClaim(new Claim(ClaimTypes.GivenName, payload.RootElement.GetProperty("givenname").GetString() ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim(ClaimTypes.Surname, payload.RootElement.GetProperty("familyname").GetString() ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim(ClaimTypes.Email, payload.RootElement.GetProperty("email").GetString() ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim("picture", payload.RootElement.GetProperty("picture").GetString() ?? "", ClaimValueTypes.String));

        return new ClaimsPrincipal(identity);
    }
}
