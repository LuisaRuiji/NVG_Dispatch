using NVGInventory.Domain.Exceptions;

namespace NVGInventory.Modules.Dispatching.Services;

public sealed record StoredTripDocument(string StorageKey, string OriginalFileName, string ContentType, long SizeBytes);
public sealed record ReadableTripDocument(Stream Content, string ContentType, string FileName);

public interface ITripDocumentStorage
{
    Task<StoredTripDocument> SaveAsync(Guid customerId, Guid tripId, Stream content, string fileName, string? contentType, long sizeBytes, CancellationToken cancellationToken = default);
    Task<ReadableTripDocument> OpenReadAsync(string storageKey, string? originalFileName, string? contentType, CancellationToken cancellationToken = default);
}

public sealed class LocalTripDocumentStorage : ITripDocumentStorage
{
    private const long MaxFileSizeBytes = 15 * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string> AllowedExtensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png"
    };
    private readonly IWebHostEnvironment _environment;

    public LocalTripDocumentStorage(IWebHostEnvironment environment) => _environment = environment;

    public async Task<StoredTripDocument> SaveAsync(Guid customerId, Guid tripId, Stream content, string fileName, string? contentType, long sizeBytes, CancellationToken cancellationToken = default)
    {
        if (sizeBytes <= 0 || sizeBytes > MaxFileSizeBytes) throw new BusinessRuleViolationException("Trip documents must be between 1 byte and 15 MB.");
        var originalName = Path.GetFileName(fileName);
        var extension = Path.GetExtension(originalName);
        if (!AllowedExtensions.TryGetValue(extension, out var safeContentType)) throw new BusinessRuleViolationException("Only PDF, JPG, and PNG trip documents can be uploaded.");
        var relativeDirectory = Path.Combine("trip-documents", customerId.ToString("N"), tripId.ToString("N"));
        var directory = Path.Combine(_environment.ContentRootPath, "App_Data", relativeDirectory);
        Directory.CreateDirectory(directory);
        var storageKey = Path.Combine(relativeDirectory, $"{Guid.NewGuid():N}{extension}").Replace('\\', '/');
        var destination = Path.Combine(_environment.ContentRootPath, "App_Data", storageKey);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, true);
        await content.CopyToAsync(output, cancellationToken);
        return new StoredTripDocument(storageKey, originalName, safeContentType, sizeBytes);
    }

    public Task<ReadableTripDocument> OpenReadAsync(string storageKey, string? originalFileName, string? contentType, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "App_Data", "trip-documents"));
        var candidate = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "App_Data", storageKey.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(candidate))
            throw new NotFoundException("Trip document file not found.");
        var extension = Path.GetExtension(candidate);
        var resolvedContentType = contentType ?? AllowedExtensions.GetValueOrDefault(extension, "application/octet-stream");
        var resolvedFileName = string.IsNullOrWhiteSpace(originalFileName) ? $"trip-document{extension}" : Path.GetFileName(originalFileName);
        Stream stream = new FileStream(candidate, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(new ReadableTripDocument(stream, resolvedContentType, resolvedFileName));
    }
}
