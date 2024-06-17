using System.Threading;
using System.Threading.Tasks;
using LivestreamRecorder.DB.Models;

namespace LivestreamRecorderBackend.Interfaces;

public interface IStorageService
{
    Task<bool> DeleteVideoBlobAsync(string filename, CancellationToken cancellation = default);
    Task UploadPublicFileAsync(string? contentType, string pathInStorage, string filePathToUpload, CancellationToken cancellation = default);
    Task<string> GetTokenAsync(Video video);
}
