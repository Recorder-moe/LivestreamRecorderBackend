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
    private readonly HttpClient _httpClient;

    public DiscordService(
        IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("client");
    }

    public async Task<ClaimsPrincipal> GetUserInfoFromTokenAsync(string token)
    {
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer " + token);
        var response = await _httpClient.GetAsync("https://discord.com/api/users/@me");
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"An error occurred when retrieving Discord user information ({response.StatusCode}). Please check if the authentication information is correct.");
        }

        string json = await response.Content.ReadAsStringAsync();
        var payload = JsonDocument.Parse(json);
        var identity = new ClaimsIdentity("discord");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, payload.RootElement.GetString("id") ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim(ClaimTypes.Name, payload.RootElement.GetString("username") ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim(ClaimTypes.Email, payload.RootElement.GetString("email") ?? "", ClaimValueTypes.String));
        identity.AddClaim(new Claim("picture", $"https://cdn.discordapp.com/avatars/{payload.RootElement.GetString("id")}/{payload.RootElement.GetString("avatar")}.png" ?? "", ClaimValueTypes.String));

        return new ClaimsPrincipal(identity);
    }
}
