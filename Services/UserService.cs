using Gravatar;
using LivestreamRecorderBackend.DB.Core;
using LivestreamRecorderBackend.DB.Exceptions;
using LivestreamRecorderBackend.DB.Models;
using LivestreamRecorderBackend.DTO.User;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Omu.ValueInjecter;
using Serilog;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace LivestreamRecorderBackend.Services;

public class UserService : IDisposable
{
    private static ILogger Logger => Helper.Log.Logger;
    private readonly UnitOfWork _unitOfWork;
    private readonly UserRepository _userRepository;
    private bool _disposedValue;

    public UserService()
    {
        string? connectionString = Environment.GetEnvironmentVariable("ConnectionStrings_Private");
        if (string.IsNullOrEmpty(connectionString)) throw new InvalidOperationException("Database connectionString is not set!!");
        var contextOptions = new DbContextOptionsBuilder<PrivateContext>()
                                     .UseCosmos(connectionString: connectionString,
                                                databaseName: "Private")
                                     .Options;
        var context = new PrivateContext(contextOptions);
        _unitOfWork = new UnitOfWork(context);
        _userRepository = new UserRepository(_unitOfWork);
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
        string ? authType = claimsPrincipal.Identity?.AuthenticationType;

        User? user = null;

        try
        {
            user = GetUserFromClaimsPrincipal(claimsPrincipal);
        }
        catch (EntityNotFoundException) {
            user = MigrateUser(claimsPrincipal);
        }

        string? userEmail = user?.Email ?? claimsPrincipal.Identity?.Name;
        string? userPicture = userEmail?.ToGravatar(200);

        if (null == user)
        {
            // New user
            user = new User()
            {
                id = Guid.NewGuid().ToString(),
                UserName = userName ?? "Valuable User",
                Email = userEmail ?? throw new InvalidOperationException("Email is empty!!"),
                Picture = userPicture,
                RegistrationDate = DateTime.Now,
                Tokens = new Tokens()
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
                    user.MicrosoftUID= uid;
                    break;
                default:
                    Logger.Error("Authentication Type {authType} is not support!!", authType);
                    throw new NotSupportedException($"Authentication Type {authType} is not support!!");
            }

            // Prevent GUID conflicts
            if (_userRepository.Exists(user.id)) user.id = Guid.NewGuid().ToString();

            _userRepository.Add(user);
        }
        else if (user.Picture != userPicture)
        {
            user.Picture = userPicture;
            _userRepository.Update(user);
        }
        _unitOfWork.Commit();
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
            if (_userRepository.Where(p => p.Email == request.Email).Any())
            {
                throw new InvalidOperationException("Email is already exists.");
            }
            user.Email = request.Email;
        }
        user.Note = request.Note ?? user.Note;

        user.Picture = user.Email?.ToGravatar(200) ?? user.Picture;

        var entry = _userRepository.Update(user);
        _unitOfWork.Commit();
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
    internal static string? GetUID(ClaimsPrincipal principal)
    {
        var uid = principal.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        if (string.IsNullOrEmpty(uid))
        {
            Helper.Log.LogClaimsPrincipal(principal);
            Logger.Error("UID is null!");
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
                Logger.Error("Authentication Type {authType} is not support!!", authType);
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
                    Logger.Warning("Migrate user {email} from {AuthType} {OldUID} to {newUID}", principal.Identity.Name, authType, user.GoogleUID, uid);
                    user.GoogleUID = uid;
                    break;
                case "github":
                    Logger.Warning("Migrate user {email} from {AuthType} {OldUID} to {newUID}", principal.Identity.Name, authType, user.GithubUID, uid);
                    user.GithubUID = uid;
                    break;
                case "aad":
                    Logger.Warning("Migrate user {email} from {AuthType} {OldUID} to {newUID}", principal.Identity.Name, authType, user.MicrosoftUID, uid);
                    user.MicrosoftUID = uid;
                    break;
                default:
                    Logger.Error("Authentication Type {authType} is not support!!", authType);
                    throw new NotSupportedException($"Authentication Type {authType} is not support!!");
            }
        }
        return user;
    }

    #region Dispose
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _unitOfWork.Dispose();
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        // 請勿變更此程式碼。請將清除程式碼放入 'Dispose(bool disposing)' 方法
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    #endregion
}
