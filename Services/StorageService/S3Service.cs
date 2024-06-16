using System;
using System.Threading;
using System.Threading.Tasks;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderBackend.Interfaces;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using Serilog;

namespace LivestreamRecorderBackend.Services.StorageService;

public class S3Service(IMinioClient minioClient,
                       ILogger logger) : IStorageService
{
    private readonly string _bucketNamePrivate = Environment.GetEnvironmentVariable("S3_BucketNamePrivate")!;
    private readonly string _bucketNamePublic = Environment.GetEnvironmentVariable("S3_BucketNamePublic")!;

    public async Task<bool> DeleteVideoBlobAsync(string filename, CancellationToken cancellation = default)
    {
        try
        {
            await minioClient.RemoveObjectAsync(new RemoveObjectArgs()
                                                .WithBucket(_bucketNamePrivate)
                                                .WithObject($"videos/{filename}"),
                                                cancellation);

            return true;
        }
        catch (MinioException e)
        {
            logger.Error(e, "Failed to delete video file: {filename}", filename);
            return false;
        }
    }

    public async Task UploadPublicFileAsync(string? contentType, string pathInStorage, string tempPath, CancellationToken cancellation = default)
    {
        try
        {
            await minioClient.PutObjectAsync(new PutObjectArgs()
                                             .WithBucket(_bucketNamePublic)
                                             .WithObject(pathInStorage)
                                             .WithFileName(tempPath)
                                             .WithContentType(contentType),
                                             cancellation);
        }
        catch (MinioException e)
        {
            logger.Error(e, "Failed to upload public file: {filePath}", pathInStorage);
        }
    }

    public async Task<string> GetTokenAsync(Video video)
    {
        try
        {
            string? url = await minioClient.PresignedGetObjectAsync(new PresignedGetObjectArgs()
                                                                    .WithBucket(_bucketNamePrivate)
                                                                    .WithObject($"videos/{video.Filename}")
                                                                    .WithRequestDate(DateTime.UtcNow.AddMinutes(-1))
                                                                    .WithExpiry((int)TimeSpan.FromHours(12).TotalSeconds));

            return new Uri(url).Query;
        }
        catch (MinioException e)
        {
            logger.Error(e, "Failed to get token for video: {video}", video);
            return string.Empty;
        }
    }
}
