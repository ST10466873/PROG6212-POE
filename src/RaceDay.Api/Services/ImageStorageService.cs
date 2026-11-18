using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.StaticFiles;

namespace RaceDay.Api.Services;

/// <summary>Result of persisting an uploaded image through the API.</summary>
/// <param name="Url">URL consumed by the MVC application (absolute Azure URL or API-relative path).</param>
/// <param name="StoragePath">Provider-specific path used to read the file again.</param>
/// <param name="StorageProvider">"AzureBlobStorage" or "LocalDevelopment" — shown in Swagger responses.</param>
public sealed record StoredImage(string Url, string StoragePath, string StorageProvider);

/// <summary>
/// Abstraction over image persistence. The MVC layer never sees this service: every
/// upload is sent to the API, and the API alone decides where the bytes are stored.
/// </summary>
public interface IImageStorageService
{
    /// <summary>True when an Azure Storage connection string has been configured.</summary>
    bool UsesAzure { get; }

    /// <summary>Stores one image under a folder such as "events" or "profiles".</summary>
    Task<StoredImage> SaveAsync(IFormFile file, string folder, CancellationToken ct = default);

    /// <summary>Opens a previously stored image for the public read endpoint.</summary>
    Task<(Stream Stream, string ContentType, string FileName)?> OpenAsync(string path, CancellationToken ct = default);
}

/// <summary>
/// Azure Blob Storage implementation with a local development fallback. The fallback keeps
/// the application runnable in CI, Docker and classroom demos where no Azure account exists;
/// setting AzureStorage__ConnectionString switches the same code path to real blob storage.
/// </summary>
public class ImageStorageService : IImageStorageService
{
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _env;
    private readonly string? _connectionString;
    private readonly string _containerName;
    private readonly string _localRoot;
    private readonly FileExtensionContentTypeProvider _contentTypes = new();

    /// <summary>Reads storage configuration from appsettings.json / environment variables.</summary>
    public ImageStorageService(IConfiguration config, IHostEnvironment env)
    {
        _config = config;
        _env = env;
        _connectionString = config["AzureStorage:ConnectionString"];
        _containerName = config["AzureStorage:ContainerName"] ?? "raceday-images";
        _localRoot = Path.Combine(env.ContentRootPath, "uploads");
    }

    /// <inheritdoc />
    public bool UsesAzure => !string.IsNullOrWhiteSpace(_connectionString);

    /// <inheritdoc />
    public async Task<StoredImage> SaveAsync(IFormFile file, string folder, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var blobName = $"{Guid.NewGuid():N}{extension}";
        var storagePath = $"{folder}/{blobName}";

        if (UsesAzure)
        {
            // Real Azure path: container is created on first use and blobs are private.
            // Read access is still mediated by the API's /api/files endpoint.
            var container = new BlobServiceClient(_connectionString).GetBlobContainerClient(_containerName);
            await container.CreateIfNotExistsAsync(cancellationToken: ct);
            var blob = container.GetBlobClient(storagePath);
            using var stream = file.OpenReadStream();
            await blob.UploadAsync(stream, new BlobHttpHeaders { ContentType = file.ContentType }, cancellationToken: ct);
            return new StoredImage(blob.Uri.ToString(), storagePath, "AzureBlobStorage");
        }

        // Development fallback: same interface, files served through /api/files.
        var directory = Path.Combine(_localRoot, folder);
        Directory.CreateDirectory(directory);
        var fullPath = Path.Combine(directory, blobName);
        using (var output = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(output, ct);
        }
        return new StoredImage(storagePath, storagePath, "LocalDevelopment");
    }

    /// <inheritdoc />
    public async Task<(Stream Stream, string ContentType, string FileName)?> OpenAsync(string path, CancellationToken ct = default)
    {
        // Reject absolute URLs and traversal attempts before touching the file system or blob client.
        if (string.IsNullOrWhiteSpace(path) || path.Contains("..") || path.Contains("://")) return null;

        if (UsesAzure)
        {
            var blob = new BlobServiceClient(_connectionString).GetBlobContainerClient(_containerName).GetBlobClient(path);
            if (!await blob.ExistsAsync(ct)) return null;
            var download = await blob.DownloadAsync(ct);
            var contentType = download.Value.ContentType;
            if (string.IsNullOrWhiteSpace(contentType))
                _contentTypes.TryGetContentType(Path.GetFileName(path), out contentType);
            return (download.Value.Content, contentType ?? "application/octet-stream", Path.GetFileName(path));
        }

        var root = Path.GetFullPath(_localRoot) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(_localRoot, path));
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(fullPath)) return null;
        _contentTypes.TryGetContentType(Path.GetFileName(fullPath), out var localType);
        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        return (stream, localType ?? "application/octet-stream", Path.GetFileName(fullPath));
    }
}
