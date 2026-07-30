using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[AllowAnonymous]
[Route("media/exercises")]
public class ExercisePublishedMediaController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ExerciseMediaStorageService _storageService;

    public ExercisePublishedMediaController(
        AppDbContext dbContext,
        ExerciseMediaStorageService storageService)
    {
        _dbContext = dbContext;
        _storageService = storageService;
    }

    [HttpGet("{exerciseId:int}/drafts/{draftId:int}/{fileName}")]
    public async Task<IActionResult> GetLegacyPublishedDraftMedia(
        int exerciseId,
        int draftId,
        string fileName,
        CancellationToken cancellationToken)
    {
        var draft = await _dbContext.ExerciseMediaDrafts
            .AsNoTracking()
            .Include(item => item.ExerciseCatalogItem)
            .FirstOrDefaultAsync(
                item =>
                    item.Id == draftId &&
                    item.ExerciseCatalogItemId == exerciseId &&
                    item.Status == ExerciseMediaDraftStatuses.Published,
                cancellationToken);
        if (draft?.ExerciseCatalogItem is null)
        {
            return NotFound();
        }

        var overrideUrl = string.Equals(fileName, "video.mp4", StringComparison.OrdinalIgnoreCase)
            ? draft.ExerciseCatalogItem.LocalVideoUrlOverride
            : draft.ExerciseCatalogItem.LocalThumbnailUrlOverride;
        if (!ReferencesRequestPath(overrideUrl, Request.Path))
        {
            return NotFound();
        }

        ExerciseMediaReadFile? media;
        try
        {
            media = _storageService.OpenLegacyDraftMedia(exerciseId, draftId, fileName);
        }
        catch (ExerciseMediaStorageException)
        {
            return NotFound();
        }

        if (media is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "public, max-age=3600";
        Response.Headers.Append("X-Content-Type-Options", "nosniff");
        return File(
            media.Content,
            media.ContentType,
            enableRangeProcessing: true);
    }

    private static bool ReferencesRequestPath(string? mediaUrl, PathString requestPath)
    {
        if (string.IsNullOrWhiteSpace(mediaUrl) || string.IsNullOrWhiteSpace(requestPath.Value))
        {
            return false;
        }

        string referencedPath;
        if (Uri.TryCreate(mediaUrl.Trim(), UriKind.Absolute, out var absoluteUri))
        {
            referencedPath = absoluteUri.AbsolutePath;
        }
        else
        {
            referencedPath = mediaUrl.Trim().Split('?', '#')[0];
        }

        return string.Equals(referencedPath, requestPath.Value, StringComparison.Ordinal);
    }
}
