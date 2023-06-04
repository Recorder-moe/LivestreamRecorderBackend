using Gravatar;
using LivestreamRecorder.DB.Core;
using LivestreamRecorder.DB.Exceptions;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderBackend.DTO.User;
using LivestreamRecorderBackend.Services.Authentication;
using Microsoft.AspNetCore.Http;
using Serilog;
using System;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LivestreamRecorderBackend.Services;

public class UserService
{
    private readonly IUnitOfWork _unitOfWork_Private;
    private readonly GoogleService _googleService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger _logger;

    public UserService(
        ILogger logger,
        IUserRepository userRepository,
        UnitOfWork_Private unitOfWork_Private,
        GoogleService googleService)
    {
        _logger = logger;
        _userRepository = userRepository;
        _unitOfWork_Private = unitOfWork_Private;
        _googleService = googleService;
    }

    internal User GetUserById(string id) => _userRepository.GetById(id);

    /// <summary>
    /// Get User by GoogleUID
    /// </summary>
    /// <param name="googleUID"></param>
    /// <returns>User</returns>
    /// <exception cref="EntityNotFoundException">User not found.</exception>
    internal User GetUserByGoogleUID(string googleUID)
        => _userRepository.Where(p => p.GoogleUID == googleUID).SingleOrDefault()
            ?? throw new EntityNotFoundException($"Entity with GoogleUID: {googleUID} was not found.");

    /// <summary>
    /// Get User by GithubUID
    /// </summary>
    /// <param name="githubUID"></param>
    /// <returns>User</returns>
    /// <exception cref="EntityNotFoundException">User not found.</exception>
    internal User GetUserByGithubUID(string githubUID)
        => _userRepository.Where(p => p.GithubUID == githubUID).SingleOrDefault()
            ?? throw new EntityNotFoundException($"Entity with GithubUID: {githubUID} was not found.");


    /// <summary>
    /// Get User by GithubUID
    /// </summary>
    /// <param name="microsoftUID"></param>
    /// <returns>User</returns>
    /// <exception cref="EntityNotFoundException">User not found.</exception>
    internal User GetUserByMicrosoftUID(string microsoftUID)
        => _userRepository.Where(p => p.MicrosoftUID == microsoftUID).SingleOrDefault()
            ?? throw new EntityNotFoundException($"Entity with MicrosoftUID: {microsoftUID} was not found.");

    internal void CreateOrUpdateUserWithOAuthClaims(ClaimsPrincipal claimsPrincipal)
    {
        string? userName = claimsPrincipal.Identity?.Name?.Split('@', StringSplitOptions.RemoveEmptyEntries)[0];
        string? authType = claimsPrincipal.Identity?.AuthenticationType;

        User? user;
        try
        {
            user = GetUserFromClaimsPrincipal(claimsPrincipal);
        }
        catch (EntityNotFoundException)
        {
            user = MigrateUser(claimsPrincipal);
        }

        string? userEmail = user?.Email ?? claimsPrincipal.Identity?.Name;
        string? userPicture = userEmail?.ToGravatar(200);

        // First user
        int UserCount = _userRepository.All().Count();
        if (null == user
            && (UserCount == 0
                || bool.Parse(Environment.GetEnvironmentVariable("Registration_allowed") ?? "false") == true))
        {
            user = new User()
            {
                id = Guid.NewGuid().ToString(),
                UserName = userName ?? "Valuable User",
                Email = userEmail ?? "",
                Picture = userPicture,
                RegistrationDate = DateTime.Now,
                IsAdmin = UserCount == 0
            };

            string? uid = GetUID(claimsPrincipal);

            switch (authType)
            {
                case "google":
                    user.GoogleUID = uid;
                    break;
                case "github":
                    user.GithubUID = uid;
                    break;
                case "aad":
                    user.MicrosoftUID = uid;
                    break;
                default:
                    _logger.Error("Authentication Type {authType} is not support!!", authType);
                    throw new NotSupportedException($"Authentication Type {authType} is not support!!");
            }

            // Prevent GUID conflicts
            if (_userRepository.Exists(user.id)) user.id = Guid.NewGuid().ToString();

            _userRepository.Add(user);
        }
        else if (null == user)
        {
            throw new EntityNotFoundException($"Cannot create new user {claimsPrincipal.Identity?.Name}. Registration for this site is not permitted.");
        }

        if (user.Picture != userPicture)
        {
            user.Picture = userPicture;
            _userRepository.Update(user);
        }

        _unitOfWork_Private.Commit();
    }

    /// <summary>
    /// Update user
    /// </summary>
    /// <param name="request">Patch update request.</param>
    /// <param name="user">User to updated.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">User id is not match.</exception>
    internal User UpdateUser(UpdateUserRequest request, User user)
    {
        if (user.id != request.id)
        {
            throw new InvalidOperationException("User id is not match!!");
        }

        user = _userRepository.GetById(user.id);
        user.UserName = request.UserName ?? user.UserName;
        // Only update if email invalid
        if (!ValidateEmail(user.Email)
            && !string.IsNullOrWhiteSpace(request.Email)
            && ValidateEmail(request.Email))
        {
            if (_userRepository.Where(p => p.Email == request.Email).ToList().Any())
            {
                throw new InvalidOperationException("Email is already exists.");
            }
            user.Email = request.Email;
        }
        user.Note = request.Note ?? user.Note;

        user.Picture = user.Email?.ToGravatar(200) ?? user.Picture;

        var entry = _userRepository.Update(user);
        _unitOfWork_Private.Commit();
        return entry.Entity;

        static bool ValidateEmail(string email_string)
        {
            // https://github.com/microsoft/referencesource/blob/master/System.ComponentModel.DataAnnotations/DataAnnotations/EmailAddressAttribute.cs#LL54C11-L54C11
            string pattern = @"^((([a-z]|\d|[!#\$%&'\*\+\-\/=\?\^_`{\|}~]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])+(\.([a-z]|\d|[!#\$%&'\*\+\-\/=\?\^_`{\|}~]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])+)*)|((\x22)((((\x20|\x09)*(\x0d\x0a))?(\x20|\x09)+)?(([\x01-\x08\x0b\x0c\x0e-\x1f\x7f]|\x21|[\x23-\x5b]|[\x5d-\x7e]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(\\([\x01-\x09\x0b\x0c\x0d-\x7f]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF]))))*(((\x20|\x09)*(\x0d\x0a))?(\x20|\x09)+)?(\x22)))@((([a-z]|\d|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(([a-z]|\d|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])([a-z]|\d|-|\.|_|~|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])*([a-z]|\d|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])))\.)+(([a-z]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(([a-z]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])([a-z]|\d|-|\.|_|~|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])*([a-z]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])))\.?$";
            bool result = Regex.IsMatch(email_string, pattern, RegexOptions.IgnoreCase);
            return result;
        }
    }

    /// <summary>
    /// Check if UID exists.
    /// </summary>
    /// <param name="principal"></param>
    /// <exception cref="InvalidOperationException"></exception>
    internal string? GetUID(ClaimsPrincipal principal)
    {
        var uid = principal.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        if (string.IsNullOrEmpty(uid))
        {
            Helper.Log.LogClaimsPrincipal(principal);
            _logger.Error("UID is null!");
        }
        return uid;
    }

    /// <summary>
    /// Get User from ClaimsPrincipal
    /// </summary>
    /// <param name="principal"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException">Issuer not supported.</exception>
    /// <exception cref="InvalidOperationException">UID is null.</exception>
    /// <exception cref="EntityNotFoundException">User not found.</exception>
    internal User GetUserFromClaimsPrincipal(ClaimsPrincipal principal)
    {
        var authType = principal.Identity!.AuthenticationType;
        var uid = GetUID(principal);
        if (string.IsNullOrEmpty(uid)) throw new InvalidOperationException("UID is null!");
        switch (authType)
        {
            case "google":
                return GetUserByGoogleUID(uid!);
            case "github":
                return GetUserByGithubUID(uid!);
            case "aad":
                return GetUserByMicrosoftUID(uid!);
            default:
                _logger.Error("Authentication Type {authType} is not support!!", authType);
                throw new NotSupportedException($"Authentication Type {authType} is not support!!");
        }
    }

    private User? MigrateUser(ClaimsPrincipal principal)
    {
        var authType = principal.Identity!.AuthenticationType;
        var uid = GetUID(principal);

        if (null == principal.Identity.Name) return null;

        var user = _userRepository.Where(p => p.Email.Contains(principal.Identity.Name)).SingleOrDefault();
        if (null != user)
        {
            switch (authType)
            {
                case "google":
                    _logger.Warning("Migrate user {email} from {AuthType} {OldUID} to {newUID}", principal.Identity.Name, authType, user.GoogleUID, uid);
                    user.GoogleUID = uid;
                    break;
                case "github":
                    _logger.Warning("Migrate user {email} from {AuthType} {OldUID} to {newUID}", principal.Identity.Name, authType, user.GithubUID, uid);
                    user.GithubUID = uid;
                    break;
                case "aad":
                    _logger.Warning("Migrate user {email} from {AuthType} {OldUID} to {newUID}", principal.Identity.Name, authType, user.MicrosoftUID, uid);
                    user.MicrosoftUID = uid;
                    break;
                default:
                    _logger.Error("Authentication Type {authType} is not support!!", authType);
                    throw new NotSupportedException($"Authentication Type {authType} is not support!!");
            }
        }
        return user;
    }

    public async Task<User?> AuthAndGetUserAsync(IHeaderDictionary headers)
    {
        if (!headers.TryGetValue("Authorization", out var authHeader)
            || authHeader.Count == 0) return null;
        var token = authHeader.First().Split(" ", StringSplitOptions.RemoveEmptyEntries).Last();
        return AuthAndGetUser(await _googleService.GetUserInfoFromTokenAsync(token));
    }

    public async Task<User?> AuthAndGetUserAsync(string token)
        => AuthAndGetUser(await _googleService.GetUserInfoFromTokenAsync(token));

    public User? AuthAndGetUser(ClaimsPrincipal principal)
    {
#if DEBUG
        Helper.Log.LogClaimsPrincipal(principal);
#endif

        try
        {
            return null != principal
                && null != principal.Identity
                && principal.Identity.IsAuthenticated
                ? GetUserFromClaimsPrincipal(principal)
                : null;
        }
        catch (Exception e)
        {
            if (e is NotSupportedException or EntityNotFoundException)
            {
                _logger.Error(e, "User not found!!");
                return null;
            }
            else
            {
                throw;
            }
        }
    }
}
