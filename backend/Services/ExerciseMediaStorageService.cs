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
    public string PublicUrl { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
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

    private static readonly IReadOnlyDictionary<string, string> ThumbnailContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp",
        };

    private readonly string _rootPath;
    private readonly string _rootPathPrefix;
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
        if (exerciseId <= 0 || draftId <= 0)
        {
            throw new ExerciseMediaStorageException("Exercise and draft identifiers must be positive.");
        }

        if (content is null || !content.CanRead)
        {
            throw new ExerciseMediaStorageException("Generated media content is unavailable.");
        }

        var normalizedContentType = NormalizeContentType(contentType);
        var extension = GetAllowedExtension(mediaKind, normalizedContentType);
        ValidateSourceFileName(sourceFileName, normalizedContentType, extension);

        var fileName = mediaKind == ExerciseMediaStorageKind.Video
            ? "video.mp4"
            : $"thumbnail{extension}";
        var draftDirectory = GetDraftDirectoryPath(exerciseId, draftId);
        var targetPath = GetSafePath(draftDirectory, fileName);
        var temporaryPath = GetSafePath(draftDirectory, $".{fileName}.{Guid.NewGuid():N}.tmp");

        Directory.CreateDirectory(draftDirectory);
        try
        {
            byte[] signatureBuffer = new byte[16];
            var signatureLength = 0;
            var bytesWritten = 0L;
            var buffer = ArrayPool<byte>.Shared.Rent(CopyBufferSize);

            try
            {
                await using var destination = new FileStream(
                    temporaryPath,
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

            ValidateFileSignature(mediaKind, normalizedContentType, signatureBuffer.AsSpan(0, signatureLength));
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
        catch (IOException)
        {
            TryDeleteTemporaryFile(temporaryPath);
            throw new ExerciseMediaStorageException("Generated media could not be stored.");
        }
        catch (UnauthorizedAccessException)
        {
            TryDeleteTemporaryFile(temporaryPath);
            throw new ExerciseMediaStorageException("Generated media could not be stored.");
        }

        return new ExerciseMediaStoredFile
        {
            PublicUrl = $"{_publicBaseUrl}/exercises/{exerciseId}/drafts/{draftId}/{fileName}",
            ContentType = normalizedContentType,
        };
    }

    private string GetDraftDirectoryPath(int exerciseId, int draftId)
    {
        return GetSafePath(_rootPath, "exercises", exerciseId.ToString(), "drafts", draftId.ToString());
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
        catch (IOException)
        {
            // A failed cleanup must not mask the safe storage error.
        }
        catch (UnauthorizedAccessException)
        {
            // A failed cleanup must not mask the safe storage error.
        }
    }
}
