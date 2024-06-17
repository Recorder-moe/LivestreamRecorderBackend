using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using LivestreamRecorderBackend.Interfaces;

namespace LivestreamRecorderBackend.Services.Authentication;

public class DiscordService(IHttpClientFactory httpClientFactory) : IAuthenticationHandlerService
{
    public async Task<ClaimsPrincipal> GetUserInfoFromTokenAsync(string token)
    {
        HttpClient httpClient = httpClientFactory.CreateClient("client");
        httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
        HttpResponseMessage response = await httpClient.GetAsync("https://discord.com/api/users/@me");
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"An error occurred when retrieving Discord user information ({response.StatusCode}). Please check if the authentication information is correct.");

        string json = await response.Content.ReadAsStringAsync();
        var payload = JsonDocument.Parse(json);
        var identity = new ClaimsIdentity("discord");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, payload.RootElement.GetProperty("id").GetString() ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim(ClaimTypes.Name, payload.RootElement.GetProperty("username").GetString() ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim(ClaimTypes.Email, payload.RootElement.GetProperty("email").GetString() ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim("picture",
                                    $"https://cdn.discordapp.com/avatars/{payload.RootElement.GetProperty("id").GetString()}/{payload.RootElement.GetProperty("avatar").GetString()}.png",
                                    ClaimValueTypes.String));

        return new ClaimsPrincipal(identity);
    }
}
