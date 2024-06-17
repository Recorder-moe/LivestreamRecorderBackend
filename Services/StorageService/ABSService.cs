using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderBackend.Interfaces;

namespace LivestreamRecorderBackend.Services.StorageService;

public class AbsService(BlobServiceClient blobServiceClient) : IStorageService
{
    private readonly BlobContainerClient _containerClientPrivate =
        blobServiceClient.GetBlobContainerClient(Environment.GetEnvironmentVariable("Blob_ContainerNamePrivate"));

    private readonly BlobContainerClient _containerClientPublic =
        blobServiceClient.GetBlobContainerClient(Environment.GetEnvironmentVariable("Blob_ContainerNamePublic"));

    public async Task<bool> DeleteVideoBlobAsync(string filename, CancellationToken cancellation = default)
    {
        return (await _containerClientPrivate.GetBlobClient($"videos/{filename}")
                                             .DeleteIfExistsAsync(snapshotsOption: DeleteSnapshotsOption.IncludeSnapshots,
                                                                  cancellationToken: cancellation)).Value;
    }

    public Task UploadPublicFileAsync(string? contentType, string pathInStorage, string tempPath, CancellationToken cancellation = default)
    {
        return _containerClientPublic.GetBlobClient(pathInStorage)
                                     .UploadAsync(path: tempPath,
                                                  httpHeaders: new BlobHttpHeaders { ContentType = contentType },
                                                  accessTier: AccessTier.Hot,
                                                  cancellationToken: cancellation);
    }

    public Task<string> GetTokenAsync(Video video)
    {
        return Task.FromResult(_containerClientPrivate.GetBlobClient($"videos/{video.Filename}")
                                                      .GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(12))
                                                      .Query);
    }
}
