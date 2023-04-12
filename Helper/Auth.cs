using LivestreamRecorder.DB.Exceptions;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderBackend.Services;
using Serilog;
using System;
using System.Security.Claims;

namespace LivestreamRecorderBackend.Helper;

internal static class Auth
{
    private static ILogger Logger => Log.Logger;
    public static User? AuthAndGetUser(ClaimsPrincipal principal, bool localhost = false)
    {
        try
        {
#if DEBUG
            using var userService = new UserService();
            if (localhost)
            {
                return userService.GetUserById(Environment.GetEnvironmentVariable("ADMIN_USER_ID")!);
            }
            else
            {
                Helper.Log.LogClaimsPrincipal(principal);
                return userService.GetUserFromClaimsPrincipal(principal);
            }
#else
            if (null == principal
                || null == principal.Identity
                || !principal.Identity.IsAuthenticated) return null;

            using var userService = new UserService();
            return userService.GetUserFromClaimsPrincipal(principal);
#endif
        }
        catch (Exception e)
        {
            if (e is NotSupportedException or EntityNotFoundException)
            {
                Logger.Error(e, "User not found!!");
                Log.LogClaimsPrincipal(principal);
                return null;
            }
            else
            {
                throw;
            }
        }
    }
}
