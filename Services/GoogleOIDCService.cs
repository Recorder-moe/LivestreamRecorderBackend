using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace LivestreamRecorderBackend.Services;

public class GoogleOIDCService
{
    private string RedirectUri { get; set; }
    private string ClientId { get; }
    private string ClientSecret { get; }

    public GoogleOIDCService()
    {
        // Read redirectUri from configuration if it exists.
        RedirectUri = Environment.GetEnvironmentVariable("Google_RedirectUri")
                      ?? "http://localhost:7210/api/Auth/oidc/signin";
        ClientId = Environment.GetEnvironmentVariable("Google_ClientId")!;
        ClientSecret = Environment.GetEnvironmentVariable("Google_ClientSecret")!;

        if (string.IsNullOrEmpty(ClientId) || string.IsNullOrEmpty(ClientSecret))
            throw new InvalidOperationException("Google_ClientId and Google_ClientSecret must be set in the environment variables.");
    }

    /// <summary>
    /// Request access token with authorization code.
    /// </summary>
    /// <param name="authorization_code"></param>
    /// <returns></returns>
    public async Task<string> GetIdTokenAsync(string authorization_code)
    {
        using HttpClient client = HttpClientFactory.Create();
        AuthorizationCodeTokenRequest request = new()
        {
            Code = authorization_code,
            RedirectUri = RedirectUri,
            ClientId = ClientId,
            ClientSecret = ClientSecret,
            Scope = "openid profile email"
        };

        TokenResponse responce = await request.ExecuteAsync(client,
                                                            GoogleAuthConsts.OidcTokenUrl,
                                                            new(),
                                                            Google.Apis.Util.SystemClock.Default);

        return responce.IdToken;
    }

    /// <summary>
    /// 動態取得request URL，以產生並覆寫RedirectUri
    /// </summary>
    /// <param name="request"></param>
    /// <param name="route"></param>
    internal void SetupRedirectUri(string requestUrl, string? route = "api/Auth/oidc/signin")
    {
        Uri uri = new(requestUrl);
        string port = uri.Scheme == "https" && uri.Port == 443
                      || uri.Scheme == "http" && uri.Port == 80
                      ? ""
                      : $":{uri.Port}";
        RedirectUri = $"{uri.Scheme}://{uri.Host}{port}/{route}";
    }
}
