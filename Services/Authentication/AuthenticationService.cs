using System;
using System.Security.Claims;
using System.Threading.Tasks;
using LivestreamRecorderBackend.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace LivestreamRecorderBackend.Services.Authentication;

public class AuthenticationService(GoogleService googleService,
                                   GitHubService githubService,
                                   MicrosoftService microsoftService,
                                   DiscordService discordService,
                                   IMemoryCache memoryCache)
{
    private readonly IAuthenticationHandlerService _discordService = discordService;
    private readonly IAuthenticationHandlerService _githubService = githubService;
    private readonly IAuthenticationHandlerService _googleService = googleService;
    private readonly IAuthenticationHandlerService _microsoftService = microsoftService;

    public async Task<ClaimsPrincipal> GetUserInfoFromTokenAsync(string token)
    {
        ClaimsPrincipal? cached = memoryCache.Get<ClaimsPrincipal>(token);
        if (null != cached) return cached;

        ClaimsPrincipal? result = null;
        try
        {
            // ReSharper disable EmptyGeneralCatchClause
            try
            {
                result = await _googleService.GetUserInfoFromTokenAsync(token);
                return result;
            }
            // skipcq: CS-R1008
            catch (Exception)
            {
            }

            try
            {
                result = await _githubService.GetUserInfoFromTokenAsync(token);
                return result;
            }
            // skipcq: CS-R1008
            catch (Exception)
            {
            }

            try
            {
                result = await _microsoftService.GetUserInfoFromTokenAsync(token);
                return result;
            }
            // skipcq: CS-R1008
            catch (Exception)
            {
            }

            try
            {
                result = await _discordService.GetUserInfoFromTokenAsync(token);
                return result;
            }
            // skipcq: CS-R1008
            catch (Exception)
            {
            }
            // ReSharper restore EmptyGeneralCatchClause
        }
        finally
        {
            if (null != result)
                memoryCache.Set(token,
                                result,
                                new MemoryCacheEntryOptions
                                {
                                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
                                    Size = 1
                                });
        }

        throw new InvalidOperationException("Token not support");
    }

    /// <summary>
    ///     動態取得request URL，以產生並覆寫RedirectUri
    /// </summary>
    /// <param name="requestUrl"></param>
    /// <param name="route"></param>
    // skipcq: CS-A1000
    internal static string GetRedirectUri(string requestUrl, string route)
    {
        Uri uri = new(requestUrl);
        string port = (uri.Scheme == "https" && uri.Port == 443)
                      || (uri.Scheme == "http" && uri.Port == 80)
            ? ""
            : $":{uri.Port}";

        return $"{uri.Scheme}://{uri.Host}{port}/{route}";
    }
}
