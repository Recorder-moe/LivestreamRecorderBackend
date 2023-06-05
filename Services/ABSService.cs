﻿using Azure.Storage.Blobs;
using LivestreamRecorder.DB.Models;
using System;

namespace LivestreamRecorderBackend.Services;

public class ABSService
{
    private readonly BlobContainerClient _blobContainerClient;
    private readonly BlobContainerClient _blobContainerClient_public;

    public ABSService(
        BlobServiceClient blobServiceClient)
    {
        string? blobContainerName = Environment.GetEnvironmentVariable("Blob_ContainerName");
        string? blobContainerNamePublic = Environment.GetEnvironmentVariable("Blob_ContainerNamePublic");
        _blobContainerClient = blobServiceClient.GetBlobContainerClient(blobContainerName);
        _blobContainerClient_public = blobServiceClient.GetBlobContainerClient(blobContainerNamePublic);
    }

    /// <summary>
    /// Get the video BlobClient with videoId in the blob container.
    /// </summary>
    /// <param name="video"></param>
    /// <returns></returns>
    public BlobClient GetVideoBlob(Video video)
        => _blobContainerClient.GetBlobClient($"videos/{video.Filename}");

    /// <summary>
    /// Get the BlobClient by name in the blob container.
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public BlobClient GetPublicBlob(string name)
        => _blobContainerClient_public.GetBlobClient(name);
}
