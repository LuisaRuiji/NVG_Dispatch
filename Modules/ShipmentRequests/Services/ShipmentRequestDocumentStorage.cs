using NVGInventory.Domain.Exceptions;

namespace NVGInventory.Modules.ShipmentRequests.Services;

public sealed record StoredShipmentRequestDocument(
    string StorageKey,
    string AbsolutePath,
    string OriginalFileName,
    string ContentType,
    long SizeBytes);

public sealed record ReadableShipmentRequestDocument(
    Stream Content,
    string ContentType,
    string FileName);

public interface IShipmentRequestDocumentStorage
{
    Task<StoredShipmentRequestDocument> SaveAsync(
        Guid customerId,
        Guid requestId,
        Stream content,
        string fileName,
        string? contentType,
        long sizeBytes,
        CancellationToken cancellationToken = default);

    Task<ReadableShipmentRequestDocument> OpenReadAsync(
        string storageKey,
        string? originalFileName,
        string? contentType,
        CancellationToken cancellationToken = default);
}

public sealed class LocalShipmentRequestDocumentStorage : IShipmentRequestDocumentStorage
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string> AllowedExtensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png"
    };

    private readonly IWebHostEnvironment _environment;

    public LocalShipmentRequestDocumentStorage(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<StoredShipmentRequestDocument> SaveAsync(
        Guid customerId,
        Guid requestId,
        Stream content,
        string fileName,
        string? contentType,
        long sizeBytes,
        CancellationToken cancellationToken = default)
    {
        if (sizeBytes <= 0 || sizeBytes > MaxFileSizeBytes)
        {
            throw new BusinessRuleViolationException("Documents must be between 1 byte and 10 MB.");
        }

        var originalFileName = Path.GetFileName(fileName);
        var extension = Path.GetExtension(originalFileName);
        if (!AllowedExtensions.TryGetValue(extension, out var safeContentType))
        {
            throw new BusinessRuleViolationException("Only PDF, JPG, and PNG documents can be uploaded.");
        }

        var relativeDirectory = Path.Combine("dispatch-documents", customerId.ToString("N"), requestId.ToString("N"));
        var directory = Path.Combine(_environment.ContentRootPath, "App_Data", relativeDirectory);
        Directory.CreateDirectory(directory);
        var storageKey = Path.Combine(relativeDirectory, $"{Guid.NewGuid():N}{extension}").Replace('\\', '/');
        var destination = Path.Combine(_environment.ContentRootPath, "App_Data", storageKey);

        await using var output = File.Create(destination);
        await content.CopyToAsync(output, cancellationToken);
        return new StoredShipmentRequestDocument(storageKey, destination, originalFileName, safeContentType, sizeBytes);
    }

    public Task<ReadableShipmentRequestDocument> OpenReadAsync(
        string storageKey,
        string? originalFileName,
        string? contentType,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dataRoot = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "App_Data", "dispatch-documents"));
        var candidate = Path.GetFullPath(Path.Combine(
            _environment.ContentRootPath,
            "App_Data",
            storageKey.Replace('/', Path.DirectorySeparatorChar)));
        var allowedPrefix = dataRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(candidate))
        {
            throw new NotFoundException("Document file not found.");
        }

        var extension = Path.GetExtension(candidate);
        var resolvedContentType = !string.IsNullOrWhiteSpace(contentType)
            ? contentType
            : AllowedExtensions.GetValueOrDefault(extension, "application/octet-stream");
        var resolvedFileName = string.IsNullOrWhiteSpace(originalFileName)
            ? $"shipment-request-document{extension}"
            : Path.GetFileName(originalFileName);
        Stream stream = new FileStream(
            candidate,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(new ReadableShipmentRequestDocument(stream, resolvedContentType, resolvedFileName));
    }
}
