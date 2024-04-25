using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderBackend.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LivestreamRecorderBackend.Services.StorageService;

public class AbsService : IStorageService
{
    private readonly BlobContainerClient _containerClientPrivate;
    private readonly BlobContainerClient _containerClientPublic;

    public AbsService(
        BlobServiceClient blobServiceClient)
    {
        _containerClientPrivate = blobServiceClient.GetBlobContainerClient(Environment.GetEnvironmentVariable("Blob_ContainerNamePrivate"));
        _containerClientPublic = blobServiceClient.GetBlobContainerClient(Environment.GetEnvironmentVariable("Blob_ContainerNamePublic"));
    }

    public async Task<bool> DeleteVideoBlobAsync(string filename, CancellationToken cancellation = default)
        => (await _containerClientPrivate.GetBlobClient($"videos/{filename}")
                                         .DeleteIfExistsAsync(snapshotsOption: DeleteSnapshotsOption.IncludeSnapshots,
                                             cancellationToken: cancellation)).Value;

    public Task UploadPublicFileAsync(string? contentType, string pathInStorage, string tempPath, CancellationToken cancellation = default)
        => _containerClientPublic.GetBlobClient(pathInStorage)
                                 .UploadAsync(path: tempPath,
                                     httpHeaders: new BlobHttpHeaders { ContentType = contentType },
                                     accessTier: AccessTier.Hot,
                                     cancellationToken: cancellation);

    public Task<string> GetTokenAsync(Video video)
        => Task.FromResult(_containerClientPrivate.GetBlobClient($"videos/{video.Filename}")
                                                  .GenerateSasUri(Azure.Storage.Sas.BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(12))
                                                  .Query);
}
