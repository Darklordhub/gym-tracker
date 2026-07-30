using System.Buffers;
using backend.Configuration;
using Microsoft.Extensions.Options;

namespace backend.Services;

public enum ExerciseMediaStorageKind
{
    Video,
    Thumbnail,
}

public sealed class ExerciseMediaStoredFile
{
    public string PreviewUrl { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
}

public sealed class ExerciseMediaReadFile : IDisposable
{
    public required FileStream Content { get; init; }
    public required string ContentType { get; init; }
    public required string FileName { get; init; }

    public void Dispose()
    {
        Content.Dispose();
    }
}

public sealed class ExerciseMediaPublication : IDisposable
{
    private readonly IReadOnlyList<ExerciseMediaPublicationFile> _files;
    private readonly string _stagingDirectory;
    private bool _completed;

    internal ExerciseMediaPublication(
        IReadOnlyList<ExerciseMediaPublicationFile> files,
        string stagingDirectory,
        string? videoPublicUrl,
        string? thumbnailPublicUrl)
    {
        _files = files;
        _stagingDirectory = stagingDirectory;
        VideoPublicUrl = videoPublicUrl;
        ThumbnailPublicUrl = thumbnailPublicUrl;
    }

    public string? VideoPublicUrl { get; }
    public string? ThumbnailPublicUrl { get; }

    public void CommitFiles()
    {
        try
        {
            foreach (var file in _files)
            {
                var destinationDirectory = Path.GetDirectoryName(file.PublicPath)
                    ?? throw new ExerciseMediaStorageException("Published media path is invalid.");
                Directory.CreateDirectory(destinationDirectory);
                File.Move(file.StagingPath, file.PublicPath, overwrite: false);
                file.WasMoved = true;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DeleteMovedFiles();
            throw new ExerciseMediaStorageException("Generated media could not be published.");
        }
    }

    public void Complete()
    {
        _completed = true;
        TryDeleteDirectory(_stagingDirectory);
    }

    public void Dispose()
    {
        if (!_completed)
        {
            DeleteMovedFiles();
        }

        TryDeleteDirectory(_stagingDirectory);
    }

    private void DeleteMovedFiles()
    {
        foreach (var file in _files.Where(file => file.WasMoved))
        {
            TryDeleteFile(file.PublicPath);
            file.WasMoved = false;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Cleanup failure must not mask the publish failure.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Cleanup failure must not mask the workflow result.
        }
    }
}

internal sealed class ExerciseMediaPublicationFile
{
    public required string StagingPath { get; init; }
    public required string PublicPath { get; init; }
    public bool WasMoved { get; set; }
}

public sealed class ExerciseMediaStorageException : Exception
{
    public ExerciseMediaStorageException(string message)
        : base(message)
    {
    }
}

public class ExerciseMediaStorageService
{
    private const int CopyBufferSize = 81920;
    private const string AdminPreviewBasePath = "/api/admin/exercise-catalog/media-studio";

    private static readonly IReadOnlyDictionary<string, string> ThumbnailContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp",
        };

    private static readonly IReadOnlyDictionary<string, string> StoredContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".mp4"] = "video/mp4",
            [".jpg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp",
        };

    private readonly string _rootPath;
    private readonly string _rootPathPrefix;
    private readonly string _privateRootPath;
    private readonly string _publicRootPath;
    private readonly string _legacyRootPath;
    private readonly string _publicBaseUrl;
    private readonly long _maxFileSizeBytes;

    public ExerciseMediaStorageService(
        IOptions<ExerciseMediaStorageOptions> options,
        IWebHostEnvironment environment)
    {
        var configuredOptions = options.Value;
        _rootPath = ResolveRootPath(configuredOptions.RootPath, environment.ContentRootPath);
        _rootPathPrefix = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;
        _privateRootPath = GetSafePath(_rootPath, "private");
        _publicRootPath = GetSafePath(_rootPath, "public");
        _legacyRootPath = GetSafePath(_rootPath, "exercises");
        _publicBaseUrl = NormalizePublicBaseUrl(configuredOptions.PublicBaseUrl);

        if (configuredOptions.MaxFileSizeMb is < 1 or > 1024)
        {
            throw new InvalidOperationException("MediaStorage:MaxFileSizeMb must be between 1 and 1024.");
        }

        _maxFileSizeBytes = configuredOptions.MaxFileSizeMb * 1024L * 1024L;
    }

    public Task<ExerciseMediaStoredFile> SaveGeneratedVideoAsync(
        int exerciseId,
        int draftId,
        Stream content,
        string? contentType,
        string? sourceFileName = null,
        CancellationToken cancellationToken = default)
    {
        return SaveAsync(
            exerciseId,
            draftId,
            ExerciseMediaStorageKind.Video,
            content,
            contentType,
            sourceFileName,
            cancellationToken);
    }

    public Task<ExerciseMediaStoredFile> SaveGeneratedThumbnailAsync(
        int exerciseId,
        int draftId,
        Stream content,
        string? contentType,
        string? sourceFileName = null,
        CancellationToken cancellationToken = default)
    {
        return SaveAsync(
            exerciseId,
            draftId,
            ExerciseMediaStorageKind.Thumbnail,
            content,
            contentType,
            sourceFileName,
            cancellationToken);
    }

    public async Task<ExerciseMediaStoredFile> SaveAsync(
        int exerciseId,
        int draftId,
        ExerciseMediaStorageKind mediaKind,
        Stream content,
        string? contentType,
        string? sourceFileName = null,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifiers(exerciseId, draftId);

        if (content is null || !content.CanRead)
        {
            throw new ExerciseMediaStorageException("Generated media content is unavailable.");
        }

        var normalizedContentType = NormalizeContentType(contentType);
        var extension = GetAllowedExtension(mediaKind, normalizedContentType);
        ValidateSourceFileName(sourceFileName, normalizedContentType, extension);

        var fileName = GetStoredFileName(mediaKind, extension);
        var draftDirectory = GetPrivateDraftDirectoryPath(exerciseId, draftId);
        var targetPath = GetSafePath(draftDirectory, fileName);
        var temporaryPath = GetSafePath(draftDirectory, $".{fileName}.{Guid.NewGuid():N}.tmp");

        Directory.CreateDirectory(draftDirectory);
        try
        {
            await CopyAndValidateAsync(
                content,
                temporaryPath,
                mediaKind,
                normalizedContentType,
                cancellationToken);
            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        catch (ExerciseMediaStorageException)
        {
            TryDeleteTemporaryFile(temporaryPath);
            throw;
        }
        catch (OperationCanceledException)
        {
            TryDeleteTemporaryFile(temporaryPath);
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TryDeleteTemporaryFile(temporaryPath);
            throw new ExerciseMediaStorageException("Generated media could not be stored.");
        }

        return new ExerciseMediaStoredFile
        {
            PreviewUrl = $"{AdminPreviewBasePath}/{draftId}/{ToPreviewRouteSegment(mediaKind)}",
            ContentType = normalizedContentType,
        };
    }

    public ExerciseMediaReadFile? OpenDraftMedia(
        int exerciseId,
        int draftId,
        ExerciseMediaStorageKind mediaKind,
        string? storedReference)
    {
        ValidateIdentifiers(exerciseId, draftId);
        var path = ResolveDraftMediaPath(exerciseId, draftId, mediaKind, storedReference);
        return path is null ? null : OpenValidatedFile(path, mediaKind);
    }

    public ExerciseMediaReadFile? OpenLegacyDraftMedia(
        int exerciseId,
        int draftId,
        string fileName)
    {
        ValidateIdentifiers(exerciseId, draftId);
        if (!TryGetMediaKind(fileName, out var mediaKind))
        {
            return null;
        }

        var legacyDirectory = GetLegacyDraftDirectoryPath(exerciseId, draftId);
        var path = GetSafePath(legacyDirectory, fileName);
        return File.Exists(path) ? OpenValidatedFile(path, mediaKind) : null;
    }

    public async Task<ExerciseMediaPublication> PrepareDraftPublicationAsync(
        int exerciseId,
        int draftId,
        string? videoReference,
        string? thumbnailReference,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifiers(exerciseId, draftId);
        if (string.IsNullOrWhiteSpace(videoReference) && string.IsNullOrWhiteSpace(thumbnailReference))
        {
            throw new ExerciseMediaStorageException("Draft has no generated media to publish.");
        }

        var publicationId = Guid.NewGuid().ToString("N");
        var stagingDirectory = GetSafePath(_privateRootPath, "publication-staging", publicationId);
        var publicDirectory = GetSafePath(
            _publicRootPath,
            "exercises",
            exerciseId.ToString(),
            "published",
            draftId.ToString(),
            publicationId);
        var publicationFiles = new List<ExerciseMediaPublicationFile>();
        string? videoPublicUrl = null;
        string? thumbnailPublicUrl = null;

        Directory.CreateDirectory(stagingDirectory);
        try
        {
            if (!string.IsNullOrWhiteSpace(videoReference))
            {
                var video = await PreparePublicationFileAsync(
                    exerciseId,
                    draftId,
                    ExerciseMediaStorageKind.Video,
                    videoReference,
                    stagingDirectory,
                    publicDirectory,
                    cancellationToken);
                publicationFiles.Add(video.File);
                videoPublicUrl = BuildPublishedUrl(exerciseId, draftId, publicationId, video.FileName);
            }

            if (!string.IsNullOrWhiteSpace(thumbnailReference))
            {
                var thumbnail = await PreparePublicationFileAsync(
                    exerciseId,
                    draftId,
                    ExerciseMediaStorageKind.Thumbnail,
                    thumbnailReference,
                    stagingDirectory,
                    publicDirectory,
                    cancellationToken);
                publicationFiles.Add(thumbnail.File);
                thumbnailPublicUrl = BuildPublishedUrl(exerciseId, draftId, publicationId, thumbnail.FileName);
            }

            return new ExerciseMediaPublication(
                publicationFiles,
                stagingDirectory,
                videoPublicUrl,
                thumbnailPublicUrl);
        }
        catch
        {
            TryDeleteDirectory(stagingDirectory);
            throw;
        }
    }

    private async Task<(ExerciseMediaPublicationFile File, string FileName)> PreparePublicationFileAsync(
        int exerciseId,
        int draftId,
        ExerciseMediaStorageKind mediaKind,
        string storedReference,
        string stagingDirectory,
        string publicDirectory,
        CancellationToken cancellationToken)
    {
        var sourcePath = ResolveDraftMediaPath(exerciseId, draftId, mediaKind, storedReference);
        if (sourcePath is null)
        {
            var mediaLabel = mediaKind == ExerciseMediaStorageKind.Video ? "video" : "thumbnail";
            throw new ExerciseMediaStorageException(
                $"Draft generated {mediaLabel} is unavailable from private storage.");
        }

        var fileName = Path.GetFileName(sourcePath);
        var contentType = GetStoredContentType(fileName);
        await using var source = OpenValidatedStream(sourcePath, mediaKind, contentType);
        var stagingPath = GetSafePath(stagingDirectory, fileName);
        await CopyAndValidateAsync(source, stagingPath, mediaKind, contentType, cancellationToken);

        return (
            new ExerciseMediaPublicationFile
            {
                StagingPath = stagingPath,
                PublicPath = GetSafePath(publicDirectory, fileName),
            },
            fileName);
    }

    private async Task CopyAndValidateAsync(
        Stream content,
        string destinationPath,
        ExerciseMediaStorageKind mediaKind,
        string contentType,
        CancellationToken cancellationToken)
    {
        byte[] signatureBuffer = new byte[16];
        var signatureLength = 0;
        var bytesWritten = 0L;
        var buffer = ArrayPool<byte>.Shared.Rent(CopyBufferSize);

        try
        {
            await using var destination = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                CopyBufferSize,
                useAsync: true);

            while (true)
            {
                var read = await content.ReadAsync(buffer.AsMemory(0, CopyBufferSize), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                if (bytesWritten > _maxFileSizeBytes - read)
                {
                    throw new ExerciseMediaStorageException("Generated media exceeds the configured file size limit.");
                }

                var signatureBytesToCopy = Math.Min(signatureBuffer.Length - signatureLength, read);
                if (signatureBytesToCopy > 0)
                {
                    buffer.AsSpan(0, signatureBytesToCopy).CopyTo(signatureBuffer.AsSpan(signatureLength));
                    signatureLength += signatureBytesToCopy;
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                bytesWritten += read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        ValidateFileSignature(mediaKind, contentType, signatureBuffer.AsSpan(0, signatureLength));
    }

    private ExerciseMediaReadFile OpenValidatedFile(string path, ExerciseMediaStorageKind mediaKind)
    {
        var contentType = GetStoredContentType(path);
        var content = OpenValidatedStream(path, mediaKind, contentType);
        return new ExerciseMediaReadFile
        {
            Content = content,
            ContentType = contentType,
            FileName = Path.GetFileName(path),
        };
    }

    private FileStream OpenValidatedStream(
        string path,
        ExerciseMediaStorageKind mediaKind,
        string contentType)
    {
        FileStream? content = null;
        try
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new ExerciseMediaStorageException("Stored media file is invalid.");
            }

            content = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                CopyBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (content.Length <= 0 || content.Length > _maxFileSizeBytes)
            {
                throw new ExerciseMediaStorageException("Stored media file size is invalid.");
            }

            Span<byte> signature = stackalloc byte[16];
            var signatureLength = content.Read(signature);
            ValidateFileSignature(mediaKind, contentType, signature[..signatureLength]);
            content.Position = 0;
            return content;
        }
        catch (ExerciseMediaStorageException)
        {
            content?.Dispose();
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            content?.Dispose();
            throw new ExerciseMediaStorageException("Stored media file is unavailable.");
        }
    }

    private string? ResolveDraftMediaPath(
        int exerciseId,
        int draftId,
        ExerciseMediaStorageKind mediaKind,
        string? storedReference)
    {
        var candidateNames = GetCandidateFileNames(mediaKind, storedReference);
        var candidateDirectories = new[]
        {
            GetPrivateDraftDirectoryPath(exerciseId, draftId),
            GetLegacyDraftDirectoryPath(exerciseId, draftId),
        };

        return candidateDirectories
            .SelectMany(directory => candidateNames.Select(fileName => GetSafePath(directory, fileName)))
            .Where(File.Exists)
            .Select(path => new FileInfo(path))
            .Where(file => (file.Attributes & FileAttributes.ReparsePoint) == 0)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => file.FullName)
            .FirstOrDefault();
    }

    private string GetPrivateDraftDirectoryPath(int exerciseId, int draftId)
    {
        return GetSafePath(
            _privateRootPath,
            "exercises",
            exerciseId.ToString(),
            "drafts",
            draftId.ToString());
    }

    private string GetLegacyDraftDirectoryPath(int exerciseId, int draftId)
    {
        return GetSafePath(
            _legacyRootPath,
            exerciseId.ToString(),
            "drafts",
            draftId.ToString());
    }

    private string GetSafePath(params string[] pathSegments)
    {
        var candidatePath = Path.GetFullPath(Path.Combine(pathSegments));
        if (!candidatePath.StartsWith(_rootPathPrefix, StringComparison.Ordinal) &&
            !string.Equals(candidatePath, _rootPath, StringComparison.Ordinal))
        {
            throw new ExerciseMediaStorageException("Generated media path is invalid.");
        }

        return candidatePath;
    }

    private static string ResolveRootPath(string? configuredRootPath, string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(configuredRootPath))
        {
            throw new InvalidOperationException("MediaStorage:RootPath must be configured.");
        }

        return Path.GetFullPath(Path.IsPathRooted(configuredRootPath)
            ? configuredRootPath
            : Path.Combine(contentRootPath, configuredRootPath));
    }

    private static string NormalizePublicBaseUrl(string? configuredPublicBaseUrl)
    {
        if (!Uri.TryCreate(configuredPublicBaseUrl?.Trim(), UriKind.Absolute, out var publicBaseUrl) ||
            (publicBaseUrl.Scheme != Uri.UriSchemeHttp && publicBaseUrl.Scheme != Uri.UriSchemeHttps) ||
            publicBaseUrl.Query.Length > 0 ||
            publicBaseUrl.Fragment.Length > 0 ||
            publicBaseUrl.AbsolutePath == "/")
        {
            throw new InvalidOperationException(
                "MediaStorage:PublicBaseUrl must be an absolute HTTP or HTTPS URL with a path.");
        }

        return publicBaseUrl.ToString().TrimEnd('/');
    }

    private static string NormalizeContentType(string? contentType)
    {
        var normalizedContentType = contentType?.Split(';', 2)[0].Trim();
        if (string.IsNullOrWhiteSpace(normalizedContentType))
        {
            throw new ExerciseMediaStorageException("Generated media content type is required.");
        }

        return normalizedContentType.ToLowerInvariant();
    }

    private static string GetAllowedExtension(ExerciseMediaStorageKind mediaKind, string contentType)
    {
        if (mediaKind == ExerciseMediaStorageKind.Video &&
            string.Equals(contentType, "video/mp4", StringComparison.Ordinal))
        {
            return ".mp4";
        }

        if (mediaKind == ExerciseMediaStorageKind.Thumbnail &&
            ThumbnailContentTypes.TryGetValue(contentType, out var extension))
        {
            return extension;
        }

        throw new ExerciseMediaStorageException("Generated media type is not supported.");
    }

    private static string GetStoredContentType(string path)
    {
        var extension = Path.GetExtension(path);
        return StoredContentTypes.TryGetValue(extension, out var contentType)
            ? contentType
            : throw new ExerciseMediaStorageException("Stored media type is not supported.");
    }

    private static string GetStoredFileName(ExerciseMediaStorageKind mediaKind, string extension)
    {
        return mediaKind == ExerciseMediaStorageKind.Video
            ? "video.mp4"
            : $"thumbnail{extension}";
    }

    private static IReadOnlyList<string> GetCandidateFileNames(
        ExerciseMediaStorageKind mediaKind,
        string? storedReference)
    {
        if (mediaKind == ExerciseMediaStorageKind.Video)
        {
            return ["video.mp4"];
        }

        var candidates = new List<string>();
        var referencedExtension = GetReferenceExtension(storedReference);
        if (ThumbnailContentTypes.Values.Contains(referencedExtension, StringComparer.OrdinalIgnoreCase))
        {
            candidates.Add($"thumbnail{referencedExtension.ToLowerInvariant()}");
        }

        candidates.AddRange(["thumbnail.jpg", "thumbnail.png", "thumbnail.webp"]);
        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string GetReferenceExtension(string? storedReference)
    {
        if (string.IsNullOrWhiteSpace(storedReference))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(storedReference.Trim(), UriKind.Absolute, out var absoluteUri))
        {
            return Path.GetExtension(absoluteUri.AbsolutePath);
        }

        return Path.GetExtension(storedReference.Trim());
    }

    private static bool TryGetMediaKind(string fileName, out ExerciseMediaStorageKind mediaKind)
    {
        if (string.Equals(fileName, "video.mp4", StringComparison.OrdinalIgnoreCase))
        {
            mediaKind = ExerciseMediaStorageKind.Video;
            return true;
        }

        if (string.Equals(fileName, "thumbnail.jpg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "thumbnail.png", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "thumbnail.webp", StringComparison.OrdinalIgnoreCase))
        {
            mediaKind = ExerciseMediaStorageKind.Thumbnail;
            return true;
        }

        mediaKind = default;
        return false;
    }

    private static string ToPreviewRouteSegment(ExerciseMediaStorageKind mediaKind)
    {
        return mediaKind == ExerciseMediaStorageKind.Video ? "video" : "thumbnail";
    }

    private string BuildPublishedUrl(
        int exerciseId,
        int draftId,
        string publicationId,
        string fileName)
    {
        var publicUrl =
            $"{_publicBaseUrl}/exercises/{exerciseId}/published/{draftId}/{publicationId}/{fileName}";
        if (publicUrl.Length > 500)
        {
            throw new ExerciseMediaStorageException("Published media URL is too long.");
        }

        return publicUrl;
    }

    private static void ValidateIdentifiers(int exerciseId, int draftId)
    {
        if (exerciseId <= 0 || draftId <= 0)
        {
            throw new ExerciseMediaStorageException("Exercise and draft identifiers must be positive.");
        }
    }

    private static void ValidateSourceFileName(
        string? sourceFileName,
        string contentType,
        string expectedExtension)
    {
        if (string.IsNullOrWhiteSpace(sourceFileName))
        {
            return;
        }

        var fileName = Path.GetFileName(sourceFileName);
        if (!string.Equals(fileName, sourceFileName, StringComparison.Ordinal) ||
            sourceFileName.Contains('/') ||
            sourceFileName.Contains('\\') ||
            !IsAllowedSourceExtension(Path.GetExtension(fileName), contentType, expectedExtension))
        {
            throw new ExerciseMediaStorageException("Generated media file extension is not supported.");
        }
    }

    private static bool IsAllowedSourceExtension(
        string sourceExtension,
        string contentType,
        string expectedExtension)
    {
        return string.Equals(sourceExtension, expectedExtension, StringComparison.OrdinalIgnoreCase) ||
            (contentType == "image/jpeg" &&
             string.Equals(sourceExtension, ".jpeg", StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateFileSignature(
        ExerciseMediaStorageKind mediaKind,
        string contentType,
        ReadOnlySpan<byte> signature)
    {
        var hasExpectedSignature = mediaKind switch
        {
            ExerciseMediaStorageKind.Video =>
                signature.Length >= 8 && signature.Slice(4, 4).SequenceEqual("ftyp"u8),
            ExerciseMediaStorageKind.Thumbnail when contentType == "image/jpeg" =>
                signature.Length >= 3 && signature.Slice(0, 3).SequenceEqual(new byte[] { 0xFF, 0xD8, 0xFF }),
            ExerciseMediaStorageKind.Thumbnail when contentType == "image/png" =>
                signature.Length >= 8 && signature.Slice(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ExerciseMediaStorageKind.Thumbnail when contentType == "image/webp" =>
                signature.Length >= 12 &&
                signature.Slice(0, 4).SequenceEqual("RIFF"u8) &&
                signature.Slice(8, 4).SequenceEqual("WEBP"u8),
            _ => false,
        };

        if (!hasExpectedSignature)
        {
            throw new ExerciseMediaStorageException("Generated media content does not match its declared type.");
        }
    }

    private static void TryDeleteTemporaryFile(string temporaryPath)
    {
        try
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A failed cleanup must not mask the safe storage error.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A failed cleanup must not mask the safe storage error.
        }
    }
}
