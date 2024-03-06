using System.Threading.Tasks;

namespace LivestreamRecorderBackend.Interfaces;

public interface IAuthenticationCodeHandlerService
{
    string ClientId { get; }
    string ClientSecret { get; }

    Task<string> GetIdTokenAsync(string authorization_code, string redirectUri);
}