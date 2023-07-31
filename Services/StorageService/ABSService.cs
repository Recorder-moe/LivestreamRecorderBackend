using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderBackend.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LivestreamRecorderBackend.Services.StorageService;

public class ABSService : IStorageService
{
    private readonly BlobContainerClient _containerClient_Private;
    private readonly BlobContainerClient _containerClient_Public;

    public ABSService(
        BlobServiceClient blobServiceClient)
    {
        _containerClient_Private = blobServiceClient.GetBlobContainerClient(Environment.GetEnvironmentVariable("Blob_ContainerNamePrivate"));
        _containerClient_Public = blobServiceClient.GetBlobContainerClient(Environment.GetEnvironmentVariable("Blob_ContainerNamePublic"));
    }

    public async Task<bool> DeleteVideoBlob(string filename, CancellationToken cancellation = default)
        => (await _containerClient_Private.GetBlobClient($"videos/{filename}")
                                          .DeleteIfExistsAsync(snapshotsOption: DeleteSnapshotsOption.IncludeSnapshots,
                                                               cancellationToken: cancellation)).Value;

    public Task UploadPublicFile(string? contentType, string pathInStorage, string tempPath, CancellationToken cancellation = default)
        => _containerClient_Public.GetBlobClient(pathInStorage)
                                  .UploadAsync(path: tempPath,
                                               httpHeaders: new BlobHttpHeaders { ContentType = contentType },
                                               accessTier: AccessTier.Hot,
                                               cancellationToken: cancellation);

    public Task<string> GetToken(Video video)
        => Task.FromResult(_containerClient_Private.GetBlobClient($"videos/{video.Filename}")
                                                   .GenerateSasUri(Azure.Storage.Sas.BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(12))
                                                   .Query);
}
