using LivestreamRecorderBackend.Interfaces;
using Microsoft.AspNetCore.Authentication;
using OAuth2.Infrastructure;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LivestreamRecorderBackend.Services.Authentication;

public class DiscordService : IAuthenticationHandlerService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public DiscordService(
        IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ClaimsPrincipal> GetUserInfoFromTokenAsync(string token)
    {
        var httpClient = _httpClientFactory.CreateClient("client");
        httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
        var response = await httpClient.GetAsync("https://discord.com/api/users/@me");
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"An error occurred when retrieving Discord user information ({response.StatusCode}). Please check if the authentication information is correct.");
        }

        string json = await response.Content.ReadAsStringAsync();
        var payload = JsonDocument.Parse(json);
        var identity = new ClaimsIdentity("discord");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, payload.RootElement.GetProperty("id").GetString() ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim(ClaimTypes.Name, payload.RootElement.GetProperty("username").GetString() ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim(ClaimTypes.Email, payload.RootElement.GetProperty("email").GetString() ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim("picture", $"https://cdn.discordapp.com/avatars/{payload.RootElement.GetProperty("id").GetString()}/{payload.RootElement.GetProperty("avatar").GetString()}.png" ?? "", ClaimValueTypes.String));

        return new ClaimsPrincipal(identity);
    }
}
