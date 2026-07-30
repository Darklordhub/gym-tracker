using System.Text.Json;
using backend.Contracts;
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public class ExerciseMediaDraftService
{
    private const string InitialPromptVersion = "v1";

    private readonly AppDbContext _dbContext;
    private readonly ExerciseMediaPromptBuilderService _promptBuilder;

    public ExerciseMediaDraftService(
        AppDbContext dbContext,
        ExerciseMediaPromptBuilderService promptBuilder)
    {
        _dbContext = dbContext;
        _promptBuilder = promptBuilder;
    }

    public async Task<IReadOnlyList<ExerciseMediaDraftResponse>> ListDraftsAsync(
        CancellationToken cancellationToken = default)
    {
        var drafts = await _dbContext.ExerciseMediaDrafts
            .AsNoTracking()
            .Include(draft => draft.ExerciseCatalogItem)
            .OrderByDescending(draft => draft.CreatedAt)
            .ThenByDescending(draft => draft.Id)
            .ToListAsync(cancellationToken);

        return drafts.Select(MapDraft).ToList();
    }

    public async Task<ExerciseMediaDraftResponse?> GetDraftByIdAsync(
        int draftId,
        CancellationToken cancellationToken = default)
    {
        var draft = await _dbContext.ExerciseMediaDrafts
            .AsNoTracking()
            .Include(item => item.ExerciseCatalogItem)
            .FirstOrDefaultAsync(item => item.Id == draftId, cancellationToken);

        return draft is null ? null : MapDraft(draft);
    }

    public async Task<ExerciseMediaStudioExerciseResponse?> GetStudioExerciseAsync(
        int exerciseId,
        CancellationToken cancellationToken = default)
    {
        var exercise = await _dbContext.ExerciseCatalogItems
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == exerciseId, cancellationToken);

        if (exercise is null)
        {
            return null;
        }

        var drafts = await _dbContext.ExerciseMediaDrafts
            .AsNoTracking()
            .Where(draft => draft.ExerciseCatalogItemId == exerciseId)
            .Include(draft => draft.ExerciseCatalogItem)
            .OrderByDescending(draft => draft.CreatedAt)
            .ThenByDescending(draft => draft.Id)
            .ToListAsync(cancellationToken);

        return new ExerciseMediaStudioExerciseResponse
        {
            Id = exercise.Id,
            Name = GetEffectiveName(exercise),
            ProviderName = exercise.Name,
            Source = exercise.Source,
            ExternalId = exercise.ExternalId,
            Instructions = GetEffectiveInstructions(exercise),
            PrimaryMuscle = exercise.PrimaryMuscle,
            SecondaryMuscles = SplitList(exercise.SecondaryMuscles),
            Equipment = exercise.Equipment,
            Difficulty = exercise.Difficulty,
            VideoUrl = GetEffectiveVideoUrl(exercise),
            ProviderVideoUrl = exercise.VideoUrl,
            LocalVideoUrlOverride = exercise.LocalVideoUrlOverride,
            ThumbnailUrl = GetEffectiveThumbnailUrl(exercise),
            ProviderThumbnailUrl = exercise.ThumbnailUrl,
            LocalThumbnailUrlOverride = exercise.LocalThumbnailUrlOverride,
            LocalMediaPath = exercise.LocalMediaPath,
            IsActive = exercise.IsActive,
            IsManuallyEdited = exercise.IsManuallyEdited,
            LatestDrafts = drafts.Select(MapDraft).ToList(),
        };
    }

    public async Task<ExerciseMediaDraftResponse?> CreateDraftAsync(
        int exerciseId,
        CreateExerciseMediaDraftRequest? request,
        int createdByUserId,
        CancellationToken cancellationToken = default)
    {
        var exercise = await _dbContext.ExerciseCatalogItems
            .FirstOrDefaultAsync(item => item.Id == exerciseId, cancellationToken);

        if (exercise is null)
        {
            return null;
        }

        var requestedMediaType = string.IsNullOrWhiteSpace(request?.MediaType)
            ? ExerciseMediaDraftMediaTypes.Video
            : request.MediaType.Trim();

        if (!TryNormalizeMediaType(requestedMediaType, out var mediaType))
        {
            throw new ArgumentException("MediaType must be one of: Video, Thumbnail, Image.", nameof(request));
        }

        var snapshotCreatedAt = DateTime.UtcNow;
        var draft = new ExerciseMediaDraft
        {
            ExerciseCatalogItemId = exercise.Id,
            Status = ExerciseMediaDraftStatuses.NeedsReview,
            MediaType = mediaType,
            PromptText = _promptBuilder.BuildPrompt(exercise),
            PromptVersion = InitialPromptVersion,
            SourceSnapshotJson = JsonSerializer.Serialize(new
            {
                exerciseId = exercise.Id,
                name = exercise.Name,
                effectiveName = GetEffectiveName(exercise),
                localNameOverride = exercise.LocalNameOverride,
                source = exercise.Source,
                externalId = exercise.ExternalId,
                equipment = exercise.Equipment,
                bodyPartOrCategory = exercise.PrimaryMuscle,
                targetMuscle = exercise.PrimaryMuscle,
                secondaryMuscles = SplitList(exercise.SecondaryMuscles),
                instructions = exercise.Instructions,
                effectiveInstructions = GetEffectiveInstructions(exercise),
                videoUrl = exercise.VideoUrl,
                thumbnailUrl = exercise.ThumbnailUrl,
                localVideoUrlOverride = exercise.LocalVideoUrlOverride,
                localThumbnailUrlOverride = exercise.LocalThumbnailUrlOverride,
                localMediaPath = exercise.LocalMediaPath,
                isManuallyEdited = exercise.IsManuallyEdited,
                catalogUpdatedAt = exercise.UpdatedAt,
                snapshotCreatedAt,
            }),
            CreatedByUserId = createdByUserId,
            CreatedAt = snapshotCreatedAt,
            UpdatedAt = snapshotCreatedAt,
        };

        _dbContext.ExerciseMediaDrafts.Add(draft);
        await _dbContext.SaveChangesAsync(cancellationToken);

        draft.ExerciseCatalogItem = exercise;
        return MapDraft(draft);
    }

    private static ExerciseMediaDraftResponse MapDraft(ExerciseMediaDraft draft)
    {
        return new ExerciseMediaDraftResponse
        {
            Id = draft.Id,
            ExerciseCatalogItemId = draft.ExerciseCatalogItemId,
            ExerciseName = draft.ExerciseCatalogItem is null ? string.Empty : GetEffectiveName(draft.ExerciseCatalogItem),
            ExerciseSource = draft.ExerciseCatalogItem?.Source ?? string.Empty,
            Status = draft.Status,
            MediaType = draft.MediaType,
            PromptText = draft.PromptText,
            PromptVersion = draft.PromptVersion,
            SourceSnapshotJson = draft.SourceSnapshotJson,
            GeneratedThumbnailUrl = draft.GeneratedThumbnailUrl,
            GeneratedVideoUrl = draft.GeneratedVideoUrl,
            GenerationProvider = draft.GenerationProvider,
            GenerationModel = draft.GenerationModel,
            ProviderJobId = draft.ProviderJobId,
            ReviewNotes = draft.ReviewNotes,
            RejectionReason = draft.RejectionReason,
            CreatedByUserId = draft.CreatedByUserId,
            ReviewedByUserId = draft.ReviewedByUserId,
            PublishedByUserId = draft.PublishedByUserId,
            ErrorMessage = draft.ErrorMessage,
            CreatedAt = draft.CreatedAt,
            UpdatedAt = draft.UpdatedAt,
            GeneratedAt = draft.GeneratedAt,
            ReviewedAt = draft.ReviewedAt,
            PublishedAt = draft.PublishedAt,
        };
    }

    private static bool TryNormalizeMediaType(string mediaType, out string normalizedMediaType)
    {
        if (string.Equals(mediaType, ExerciseMediaDraftMediaTypes.Video, StringComparison.OrdinalIgnoreCase))
        {
            normalizedMediaType = ExerciseMediaDraftMediaTypes.Video;
            return true;
        }

        if (string.Equals(mediaType, ExerciseMediaDraftMediaTypes.Thumbnail, StringComparison.OrdinalIgnoreCase))
        {
            normalizedMediaType = ExerciseMediaDraftMediaTypes.Thumbnail;
            return true;
        }

        if (string.Equals(mediaType, ExerciseMediaDraftMediaTypes.Image, StringComparison.OrdinalIgnoreCase))
        {
            normalizedMediaType = ExerciseMediaDraftMediaTypes.Image;
            return true;
        }

        normalizedMediaType = string.Empty;
        return false;
    }

    private static string[] SplitList(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static string GetEffectiveName(ExerciseCatalogItem item)
    {
        return string.IsNullOrWhiteSpace(item.LocalNameOverride) ? item.Name : item.LocalNameOverride.Trim();
    }

    private static string? GetEffectiveInstructions(ExerciseCatalogItem item)
    {
        return string.IsNullOrWhiteSpace(item.LocalInstructionsOverride)
            ? item.Instructions
            : item.LocalInstructionsOverride.Trim();
    }

    private static string? GetEffectiveThumbnailUrl(ExerciseCatalogItem item)
    {
        return string.IsNullOrWhiteSpace(item.LocalThumbnailUrlOverride)
            ? item.ThumbnailUrl
            : item.LocalThumbnailUrlOverride.Trim();
    }

    private static string? GetEffectiveVideoUrl(ExerciseCatalogItem item)
    {
        return string.IsNullOrWhiteSpace(item.LocalVideoUrlOverride)
            ? item.VideoUrl
            : item.LocalVideoUrlOverride.Trim();
    }
}
