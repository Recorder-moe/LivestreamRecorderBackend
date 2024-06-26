﻿#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
#elif COUCHDB
using LivestreamRecorder.DB.CouchDB;
#endif
using System;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Gravatar;
using LivestreamRecorder.DB.Exceptions;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderBackend.DTO.User;
using LivestreamRecorderBackend.Services.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Serilog;
using Log = LivestreamRecorderBackend.Helper.Log;

namespace LivestreamRecorderBackend.Services;

public partial class UserService(ILogger logger,
                                 IUserRepository userRepository,
                                 UnitOfWork_Private unitOfWorkPrivate,
                                 AuthenticationService authenticationService)
{
#pragma warning disable CA1859
    private readonly IUnitOfWork _unitOfWorkPrivate = unitOfWorkPrivate;
#pragma warning restore CA1859

    internal Task<User?> GetUserByIdAsync(string id)
    {
        return userRepository.GetByIdAsync(id);
    }

    internal async Task CreateOrUpdateUserWithOAuthClaimsAsync(ClaimsPrincipal claimsPrincipal)
    {
        string? userName = claimsPrincipal.Identity?.Name?.Split('@', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                           ?? claimsPrincipal.FindFirst(ClaimTypes.Name)?.Value
                           ?? claimsPrincipal.FindFirst(ClaimTypes.GivenName)?.Value;

        string? authType = claimsPrincipal.Identity?.AuthenticationType;

        User? user;
        try
        {
            user = GetUserFromClaimsPrincipal(claimsPrincipal);
        }
        catch (EntityNotFoundException)
        {
            user = MigrateUser(claimsPrincipal);
            if (null != user) await userRepository.AddOrUpdateAsync(user);
        }

        string? userEmail = user?.Email
                            ?? claimsPrincipal.FindFirst(ClaimTypes.Email)?.Value
                            ?? claimsPrincipal.Identity?.Name;

        string? userPicture = userEmail?.ToGravatar(200);

        bool hasUser = await userRepository.CountAsync() > 0;
        var isRegistrationAllowed = bool.Parse(Environment.GetEnvironmentVariable("Registration_allowed") ?? "false");

        if (null == user)
        {
            // First user or registration allowed
            if (!hasUser || isRegistrationAllowed)
            {
                user = new User
                {
                    id = Guid.NewGuid().ToString().Replace("-", ""),
                    UserName = userName ?? "Valuable User",
                    Email = userEmail ?? "",
                    Picture = userPicture,
                    RegistrationDate = DateTime.UtcNow,
                    IsAdmin = !hasUser // First user is admin
                };

                string? uid = GetUID(claimsPrincipal);

                switch (authType)
                {
                    case "google":
                        user.GoogleUID = uid;
                        break;
                    case "github":
                        user.GitHubUID = uid;
                        break;
                    case "aad":
                        user.MicrosoftUID = uid;
                        break;
                    case "discord":
                        user.DiscordUID = uid;
                        break;
                    default:
                        logger.Error("Authentication Type {authType} is not support!!", authType);
                        throw new NotSupportedException($"Authentication Type {authType} is not support!!");
                }

                // Prevent GUID conflicts
                if (userRepository.Exists(user.id))
                    user.id = Guid.NewGuid().ToString().Replace("-", "");

                await userRepository.AddOrUpdateAsync(user);
            }
            else
            {
                throw new EntityNotFoundException(
                    $"Cannot create new user {claimsPrincipal.Identity?.Name}. Registration for this site is not permitted.");
            }
        }

        if (user.Picture != userPicture)
        {
            user.Picture = userPicture;
            await userRepository.AddOrUpdateAsync(user);
        }

        _unitOfWorkPrivate.Commit();
    }

    /// <summary>
    ///     Update user
    /// </summary>
    /// <param name="request">Patch update request.</param>
    /// <param name="user">User to update.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">User id is not match.</exception>
    internal async Task<User> UpdateUserAsync(UpdateUserRequest request, User user)
    {
        if (user.id != request.id) throw new InvalidOperationException("User id is not match!!");

        await userRepository.ReloadEntityFromDBAsync(user);

        user.UserName = request.UserName ?? user.UserName;
        // Only update if email invalid
        if (!validateEmail(user.Email)
            && !string.IsNullOrWhiteSpace(request.Email)
            && validateEmail(request.Email))
        {
            if (userRepository.Where(p => p.Email == request.Email).ToList().Count != 0)
                throw new InvalidOperationException("Email is already exists.");

            user.Email = request.Email;
        }

        user.Note = request.Note ?? user.Note;

        user.Picture = user.Email?.ToGravatar(200) ?? user.Picture;

        user = await userRepository.AddOrUpdateAsync(user);
        _unitOfWorkPrivate.Commit();
        return user;

        static bool validateEmail(string emailString)
        {
            bool result = EmailRegex().IsMatch(emailString);
            return result;
        }
    }

    /// <summary>
    ///     Email Regex
    /// </summary>
    /// <returns></returns>
    /// <remarks><see href="https://github.com/microsoft/referencesource/blob/master/System.ComponentModel.DataAnnotations/DataAnnotations/EmailAddressAttribute.cs#LL54C11-L54C11"/></remarks>
    [GeneratedRegex(
        // ReSharper disable StringLiteralTypo
        @"^((([a-z]|\d|[!#\$%&'\*\+\-\/=\?\^_`{\|}~]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])+(\.([a-z]|\d|[!#\$%&'\*\+\-\/=\?\^_`{\|}~]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])+)*)|((\x22)((((\x20|\x09)*(\x0d\x0a))?(\x20|\x09)+)?(([\x01-\x08\x0b\x0c\x0e-\x1f\x7f]|\x21|[\x23-\x5b]|[\x5d-\x7e]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(\\([\x01-\x09\x0b\x0c\x0d-\x7f]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF]))))*(((\x20|\x09)*(\x0d\x0a))?(\x20|\x09)+)?(\x22)))@((([a-z]|\d|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(([a-z]|\d|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])([a-z]|\d|-|\.|_|~|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])*([a-z]|\d|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])))\.)+(([a-z]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(([a-z]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])([a-z]|\d|-|\.|_|~|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])*([a-z]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])))\.?$",
        // ReSharper restore StringLiteralTypo
        RegexOptions.IgnoreCase,
        "zh-TW")]
    private static partial Regex EmailRegex();

    /// <summary>
    ///     Check if UID exists.
    /// </summary>
    /// <param name="principal"></param>
    /// <exception cref="InvalidOperationException"></exception>
    // ReSharper disable once InconsistentNaming
    private string? GetUID(ClaimsPrincipal principal)
    {
        string? uid = principal.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        if (!string.IsNullOrEmpty(uid)) return uid;

        Log.LogClaimsPrincipal(principal);
        logger.Error("UID is null!");

        return uid;
    }

    /// <summary>
    ///     Get User from ClaimsPrincipal
    /// </summary>
    /// <param name="principal"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException">Issuer not supported.</exception>
    /// <exception cref="InvalidOperationException">UID is null.</exception>
    /// <exception cref="EntityNotFoundException">User not found.</exception>
    private User GetUserFromClaimsPrincipal(ClaimsPrincipal principal)
    {
        string? authType = principal.Identity!.AuthenticationType;
        string? uid = GetUID(principal);
        if (string.IsNullOrEmpty(uid)) throw new InvalidOperationException("UID is null!");

        switch (authType)
        {
            case "google":
                return GetUserByGoogleUID(uid);
            case "github":
                return GetUserByGitHubUID(uid);
            case "aad":
                return GetUserByMicrosoftUID(uid);
            case "discord":
                return GetUserByDiscordUID(uid);
            default:
                logger.Error("Authentication Type {authType} is not support!!", authType);
                throw new NotSupportedException($"Authentication Type {authType} is not support!!");
        }
    }

    private User? MigrateUser(ClaimsPrincipal principal)
    {
        string? authType = principal.Identity!.AuthenticationType;
        string? uid = GetUID(principal);

        if (null == principal.Identity.Name) return null;

        string? email = principal.FindFirst(ClaimTypes.Email)?.Value;
        if (null == email) return null;
        User? user = userRepository.Where(p => p.Email == email).SingleOrDefault();
        if (null == user) return null;

        switch (authType)
        {
            case "google":
                logger.Warning("Migrate user {email} from {AuthType} {OldUID} to {newUID}", principal.Identity.Name, authType, user.GoogleUID, uid);
                user.GoogleUID = uid;
                break;
            case "github":
                logger.Warning("Migrate user {email} from {AuthType} {OldUID} to {newUID}", principal.Identity.Name, authType, user.GitHubUID, uid);
                user.GitHubUID = uid;
                break;
            case "aad":
                logger.Warning("Migrate user {email} from {AuthType} {OldUID} to {newUID}",
                               principal.Identity.Name,
                               authType,
                               user.MicrosoftUID,
                               uid);

                user.MicrosoftUID = uid;
                break;
            case "discord":
                logger.Warning("Migrate user {email} from {AuthType} {OldUID} to {newUID}",
                               principal.Identity.Name,
                               authType,
                               user.MicrosoftUID,
                               uid);

                user.DiscordUID = uid;
                break;
            default:
                logger.Error("Authentication Type {authType} is not support!!", authType);
                throw new NotSupportedException($"Authentication Type {authType} is not support!!");
        }

        return user;
    }

    public Task<User?> AuthAndGetUserAsync(IHeaderDictionary headers)
    {
        if (!headers.TryGetValue("Authorization", out StringValues authHeader)
            || authHeader.Count == 0) return Task.FromResult<User?>(null);

        string? token = authHeader.First()?.Split(" ", StringSplitOptions.RemoveEmptyEntries).Last();
        return null == token ? Task.FromResult<User?>(null) : AuthAndGetUserAsync(token);
    }

    private async Task<User?> AuthAndGetUserAsync(string token)
    {
        return AuthAndGetUser(await authenticationService.GetUserInfoFromTokenAsync(token));
    }

    private User? AuthAndGetUser(ClaimsPrincipal principal)
    {
#if !RELEASE
        Log.LogClaimsPrincipal(principal);
#endif

        try
        {
            return null != principal.Identity
                   && principal.Identity.IsAuthenticated
                ? GetUserFromClaimsPrincipal(principal)
                : null;
        }
        catch (Exception e)
        {
            if (e is NotSupportedException or EntityNotFoundException)
            {
                logger.Error(e, "User not found!!");
                return null;
            }

            throw;
        }
    }

    // ReSharper disable InconsistentNaming (UID)
    /// <summary>
    ///     Get User by GoogleUID
    /// </summary>
    /// <param name="googleUID"></param>
    /// <returns>User</returns>
    /// <exception cref="EntityNotFoundException">User not found.</exception>
    private User GetUserByGoogleUID(string googleUID)
    {
        return userRepository.Where(p => p.GoogleUID == googleUID).SingleOrDefault()
               ?? throw new EntityNotFoundException($"Entity with GoogleUID: {googleUID} was not found.");
    }

    /// <summary>
    ///     Get User by GitHubUID
    /// </summary>
    /// <param name="githubUID"></param>
    /// <returns>User</returns>
    /// <exception cref="EntityNotFoundException">User not found.</exception>
    private User GetUserByGitHubUID(string githubUID)
    {
        return userRepository.Where(p => p.GitHubUID == githubUID).SingleOrDefault()
               ?? throw new EntityNotFoundException($"Entity with GitHubUID: {githubUID} was not found.");
    }

    /// <summary>
    ///     Get User by GitHubUID
    /// </summary>
    /// <param name="microsoftUID"></param>
    /// <returns>User</returns>
    /// <exception cref="EntityNotFoundException">User not found.</exception>
    private User GetUserByMicrosoftUID(string microsoftUID)
    {
        return userRepository.Where(p => p.MicrosoftUID == microsoftUID).SingleOrDefault()
               ?? throw new EntityNotFoundException($"Entity with MicrosoftUID: {microsoftUID} was not found.");
    }

    /// <summary>
    ///     Get User by GitHubUID
    /// </summary>
    /// <param name="discordUID"></param>
    /// <returns>User</returns>
    /// <exception cref="EntityNotFoundException">User not found.</exception>
    private User GetUserByDiscordUID(string discordUID)
    {
        return userRepository.Where(p => p.DiscordUID == discordUID).SingleOrDefault()
               ?? throw new EntityNotFoundException($"Entity with DiscordUID: {discordUID} was not found.");
    }
    // ReSharper restore InconsistentNaming
}
