using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using LivestreamRecorderBackend.Interfaces;

namespace LivestreamRecorderBackend.Services.Authentication;

public class GoogleService(IHttpClientFactory httpClientFactory) : IAuthenticationHandlerService
{
    public async Task<ClaimsPrincipal> GetUserInfoFromTokenAsync(string token)
    {
        HttpClient httpClient = httpClientFactory.CreateClient("client");
        httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
        HttpResponseMessage response = await httpClient.GetAsync("https://www.googleapis.com/oauth2/v3/userinfo");
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"An error occurred when retrieving Google user information ({response.StatusCode}). Please check if the authentication information is correct.");

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var identity = new ClaimsIdentity("google");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, payload.RootElement.GetProperty("sub").GetString() ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim(ClaimTypes.Name, payload.RootElement.GetProperty("name").GetString() ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim(ClaimTypes.GivenName, payload.RootElement.GetProperty("given_name").GetString() ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim(ClaimTypes.Surname, payload.RootElement.GetProperty("family_name").GetString() ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim(ClaimTypes.Email, payload.RootElement.GetProperty("email").GetString() ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim("picture", payload.RootElement.GetProperty("picture").GetString() ?? "", ClaimValueTypes.String));

        return new ClaimsPrincipal(identity);
    }
}
