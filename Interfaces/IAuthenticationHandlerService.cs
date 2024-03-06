using System.Security.Claims;
using System.Threading.Tasks;

namespace LivestreamRecorderBackend.Interfaces;

public interface IAuthenticationHandlerService
{
    Task<ClaimsPrincipal> GetUserInfoFromTokenAsync(string token);
}