using LivestreamRecorderBackend.DB.Core;
using LivestreamRecorderBackend.DB.Exceptions;
using Microsoft.EntityFrameworkCore;
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

    internal void CreateOrUpdateUser(ClaimsPrincipal claimsPrincipal)
    {
        string? userName = claimsPrincipal.FindFirst("name")?.Value;
        string? userEmail = claimsPrincipal.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value;
        string? userPicture = claimsPrincipal.FindFirst("picture")?.Value;
        string? issuer = claimsPrincipal.FindFirst("iss")?.Value;
        string? uid = claimsPrincipal.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

        // Use Google bigger picture
        if (null != userPicture) userPicture = Regex.Replace(userPicture, @"=s\d\d-c$", "=s0");

        DB.Models.User? user = null;

        switch (issuer)
        {
            case "https://accounts.google.com":
                user = _userRepository.Where(p => p.GoogleUID == uid).FirstOrDefault();
                break;
            default:
                throw new NotSupportedException($"Issuer {issuer} is not support!!");
        }

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
        else if (user.UserName != userName
                 || user.Email != userEmail
                 || user.Picture != userPicture)
        {
            user.UserName = userName ?? "Valuable User";
            user.Email = userEmail ?? throw new InvalidOperationException("Email is empty!!");
            user.Picture = userPicture;
            _userRepository.Update(user);
        }
        _unitOfWork.Commit();
    }

    internal DB.Models.User GetUserById(string id) => _userRepository.GetById(id);

    internal DB.Models.User GetUserByGoogleUID(string googleUID)
        => _userRepository.Where(p => p.GoogleUID == googleUID).SingleOrDefault()
            ?? throw new EntityNotFoundException($"Entity with GoogleUID: {googleUID} was not found.");

    internal static void CheckUID(ClaimsPrincipal principal)
    {
        var uid = principal.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        if (string.IsNullOrEmpty(uid))
        {
            Helper.Log.LogClaimsPrincipal(principal);
            Logger.Error("UID is null!");
            throw new InvalidOperationException("UID is null!!");
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
