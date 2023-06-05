using LivestreamRecorderBackend.Interfaces;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace LivestreamRecorderBackend.Services.Authentication;

public class AuthenticationService
{
    private readonly IAuthenticationHandlerService _googleService;
    private readonly IAuthenticationHandlerService _githubService;
    private readonly IAuthenticationHandlerService _microsoftService;

    public AuthenticationService(
        GoogleService googleService,
        GithubService githubService,
        MicrosoftService microsoftService)
    {
        _googleService = googleService;
        _githubService = githubService;
        _microsoftService = microsoftService;
    }

    public async Task<ClaimsPrincipal> GetUserInfoFromTokenAsync(string token)
    {
        try
        {
            return await _googleService.GetUserInfoFromTokenAsync(token);
        }
        catch (Exception) { }
        try
        {
            return await _githubService.GetUserInfoFromTokenAsync(token);
        }
        catch (Exception) { }
        try
        {
            return await _microsoftService.GetUserInfoFromTokenAsync(token);
        }
        catch (Exception) { }


        throw new InvalidOperationException("Token not support");
    }

    /// <summary>
    /// 動態取得request URL，以產生並覆寫RedirectUri
    /// </summary>
    /// <param name="request"></param>
    /// <param name="route"></param>
    internal static string GetRedirectUri(string requestUrl, string route)
    {
        Uri uri = new(requestUrl);
        string port = uri.Scheme == "https" && uri.Port == 443
                      || uri.Scheme == "http" && uri.Port == 80
                      ? ""
                      : $":{uri.Port}";
        return $"{uri.Scheme}://{uri.Host}{port}/{route}";
    }
}
