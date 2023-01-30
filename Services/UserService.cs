using LivestreamRecorderBackend.DB.Core;
using LivestreamRecorderBackend.DB.Exceptions;
using LivestreamRecorderBackend.DTO.User;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Omu.ValueInjecter;
using Serilog;
using System;
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

    internal DB.Models.User GetUserById(string id) => _userRepository.GetById(id);

    /// <summary>
    /// Get User by GoogleUID
    /// </summary>
    /// <param name="googleUID"></param>
    /// <returns>User</returns>
    /// <exception cref="EntityNotFoundException">User not found.</exception>
    internal DB.Models.User GetUserByGoogleUID(string googleUID)
        => _userRepository.Where(p => p.GoogleUID == googleUID).SingleOrDefault()
            ?? throw new EntityNotFoundException($"Entity with GoogleUID: {googleUID} was not found.");

    internal void CreateOrUpdateUserWithOAuthClaims(ClaimsPrincipal claimsPrincipal)
    {
        string? userName = claimsPrincipal.FindFirst("name")?.Value;
        string? userEmail = claimsPrincipal.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value;
        string? userPicture = claimsPrincipal.FindFirst("picture")?.Value;
        string? issuer = claimsPrincipal.FindFirst("iss")?.Value;

        // Use Google bigger picture
        if (null != userPicture) userPicture = Regex.Replace(userPicture, @"=s\d\d-c$", "=s0");

        DB.Models.User? user = null;

        try
        {
            user = GetUserFromClaimsPrincipal(claimsPrincipal);
        }
        // New user
        catch (EntityNotFoundException) { }

        if (null == user)
        {
            user = new DB.Models.User()
            {
                id = Guid.NewGuid().ToString(),
                UserName = userName ?? "Valuable User",
                Email = userEmail ?? throw new InvalidOperationException("Email is empty!!"),
                Picture = userPicture,
                RegistrationDate = DateTime.Now,
                Tokens = new DB.Models.Tokens()
            };

            if (issuer == "https://accounts.google.com")
            {
                user.GoogleUID = claimsPrincipal.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
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
    internal DB.Models.User UpdateUser(UpdateUserRequest request, DB.Models.User user)
    {
        if (user.id != request.id)
        {
            throw new InvalidOperationException("User id is not match!!");
        }

        user = _userRepository.GetById(user.id);
        user.UserName = request.UserName ?? user.UserName;
        user.Picture = request.Picture ?? user.Picture;
        user.Note = request.Note ?? user.Note;

        var entry = _userRepository.Update(user);
        _unitOfWork.Commit();
        return entry.Entity;
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
    internal DB.Models.User GetUserFromClaimsPrincipal(ClaimsPrincipal principal)
    {
        var issuer = principal.FindFirst("iss")?.Value;
        switch (issuer)
        {
            case "https://accounts.google.com":
                var uid = GetUID(principal);

                if (string.IsNullOrEmpty(uid)) throw new InvalidOperationException("UID is null!");

                return GetUserByGoogleUID(uid!);
            default:
                Logger.Error("Issuer {issuer} is not support!!", issuer);
                throw new NotSupportedException($"Issuer {issuer} is not support!!");
        }
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
