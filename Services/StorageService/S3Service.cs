using LivestreamRecorder.DB.Models;
using LivestreamRecorderBackend.Interfaces;
using Minio;
using Minio.Exceptions;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LivestreamRecorderBackend.SingletonServices;

public class S3Service : IStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly ILogger _logger;
    readonly string _bucketName_Private = Environment.GetEnvironmentVariable("S3_BucketNamePrivate")!;
    readonly string _bucketName_Public = Environment.GetEnvironmentVariable("S3_BucketNamePublic")!;

    public S3Service(
        IMinioClient minioClient,
        ILogger logger)
    {
        _minioClient = minioClient;
        _logger = logger;
    }

    public async Task<bool> DeleteVideoBlobAsync(string filename, CancellationToken cancellation = default)
    {
        try
        {
            await _minioClient.RemoveObjectAsync(new RemoveObjectArgs()
                                .WithBucket(_bucketName_Private)
                                .WithObject($"videos/{filename}"), cancellation);
            return true;
        }
        catch (MinioException e)
        {
            _logger.Error(e, "Failed to delete video file: {filename}", filename);
            return false;
        }
    }

    public async Task UploadPublicFileAsync(string? contentType, string pathInStorage, string tempPath, CancellationToken cancellation = default)
    {
        try
        {
            var response = await _minioClient.PutObjectAsync(new PutObjectArgs()
                                                .WithBucket(_bucketName_Public)
                                                .WithObject(pathInStorage)
                                                .WithFileName(tempPath)
                                                .WithContentType(contentType), cancellation);
        }
        catch (MinioException e)
        {
            _logger.Error(e, "Failed to upload public file: {filePath}", pathInStorage);
        }
    }

    public async Task<string> GetTokenAsync(Video video)
    {
        try
        {
            var url = await _minioClient.PresignedGetObjectAsync(new PresignedGetObjectArgs()
                                                .WithBucket(_bucketName_Private)
                                                .WithObject($"videos/{video.Filename}")
                                                .WithRequestDate(DateTime.UtcNow.AddMinutes(-1))
                                                .WithExpiry((int)TimeSpan.FromHours(12).TotalSeconds));
            return new Uri(url).Query;
        }
        catch (MinioException e)
        {
            _logger.Error(e, "Failed to get token for video: {video}", video);
            return string.Empty;
        }
    }
}
