using LivestreamRecorder.DB.Models;
using System.Threading;
using System.Threading.Tasks;

namespace LivestreamRecorderBackend.Interfaces;

public interface IStorageService
{
    Task<bool> DeleteVideoBlob(string filename, CancellationToken cancellation = default);
    Task UploadPublicFile(string? contentType, string pathInStorage, string filePathToUpload, CancellationToken cancellation = default);
    Task<string> GetToken(Video video);
}