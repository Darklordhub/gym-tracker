using System.Text.Json;
using backend.Configuration;
using backend.Contracts;
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Services;

public class ExerciseMediaDraftService
{
    private const string InitialPromptVersion = "v1";

    private readonly AppDbContext _dbContext;
    private readonly ExerciseMediaPromptBuilderService _promptBuilder;
    private readonly ExerciseMediaStorageService _storageService;
    private readonly IReadOnlyList<IExerciseMediaGenerationProvider> _generationProviders;
    private readonly MediaGenerationOptions _generationOptions;

    public ExerciseMediaDraftService(
        AppDbContext dbContext,
        ExerciseMediaPromptBuilderService promptBuilder,
        ExerciseMediaStorageService storageService,
        IEnumerable<IExerciseMediaGenerationProvider> generationProviders,
        IOptions<MediaGenerationOptions> generationOptions)
    {
        _dbContext = dbContext;
        _promptBuilder = promptBuilder;
        _storageService = storageService;
        _generationProviders = generationProviders.ToList();
        _generationOptions = generationOptions.Value;
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

    public async Task<ExerciseMediaReadFile?> OpenDraftMediaAsync(
        int draftId,
        ExerciseMediaStorageKind mediaKind,
        CancellationToken cancellationToken = default)
    {
        var draft = await _dbContext.ExerciseMediaDrafts
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == draftId, cancellationToken);
        if (draft is null)
        {
            return null;
        }

        var storedReference = mediaKind == ExerciseMediaStorageKind.Video
            ? draft.GeneratedVideoUrl
            : draft.GeneratedThumbnailUrl;
        if (string.IsNullOrWhiteSpace(storedReference))
        {
            return null;
        }

        try
        {
            return _storageService.OpenDraftMedia(
                draft.ExerciseCatalogItemId,
                draft.Id,
                mediaKind,
                storedReference);
        }
        catch (ExerciseMediaStorageException exception)
        {
            throw new ExerciseMediaDraftWorkflowException(exception.Message);
        }
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
        await SaveDraftChangesAsync(cancellationToken);

        draft.ExerciseCatalogItem = exercise;
        return MapDraft(draft);
    }

    public async Task<ExerciseMediaDraftResponse?> ApproveDraftAsync(
        int draftId,
        ReviewExerciseMediaDraftRequest request,
        int reviewedByUserId,
        CancellationToken cancellationToken = default)
    {
        var draft = await GetDraftForUpdateAsync(draftId, cancellationToken);
        if (draft is null)
        {
            return null;
        }

        if (!string.Equals(draft.Status, ExerciseMediaDraftStatuses.NeedsReview, StringComparison.Ordinal) &&
            !string.Equals(draft.Status, ExerciseMediaDraftStatuses.Generated, StringComparison.Ordinal))
        {
            throw new ExerciseMediaDraftWorkflowException("Only drafts that need review or have been generated can be approved.");
        }

        var now = DateTime.UtcNow;
        draft.Status = ExerciseMediaDraftStatuses.Approved;
        draft.ReviewNotes = NormalizeOptionalText(request.ReviewNotes);
        draft.RejectionReason = null;
        draft.ReviewedByUserId = reviewedByUserId;
        draft.ReviewedAt = now;
        draft.UpdatedAt = now;

        await SaveDraftChangesAsync(cancellationToken);
        return MapDraft(draft);
    }

    public async Task<ExerciseMediaDraftResponse?> RejectDraftAsync(
        int draftId,
        RejectExerciseMediaDraftRequest request,
        int reviewedByUserId,
        CancellationToken cancellationToken = default)
    {
        var draft = await GetDraftForUpdateAsync(draftId, cancellationToken);
        if (draft is null)
        {
            return null;
        }

        if (string.Equals(draft.Status, ExerciseMediaDraftStatuses.Published, StringComparison.Ordinal))
        {
            throw new ExerciseMediaDraftWorkflowException("Published drafts cannot be rejected.");
        }

        var now = DateTime.UtcNow;
        draft.Status = ExerciseMediaDraftStatuses.Rejected;
        draft.ReviewNotes = NormalizeOptionalText(request.ReviewNotes);
        draft.RejectionReason = NormalizeOptionalText(request.RejectionReason);
        draft.ReviewedByUserId = reviewedByUserId;
        draft.ReviewedAt = now;
        draft.UpdatedAt = now;

        await SaveDraftChangesAsync(cancellationToken);
        return MapDraft(draft);
    }

    public async Task<ExerciseMediaDraftResponse?> PublishDraftAsync(
        int draftId,
        int publishedByUserId,
        CancellationToken cancellationToken = default)
    {
        var draft = await GetDraftForUpdateAsync(draftId, cancellationToken);
        if (draft is null)
        {
            return null;
        }

        if (!string.Equals(draft.Status, ExerciseMediaDraftStatuses.Approved, StringComparison.Ordinal))
        {
            throw new ExerciseMediaDraftWorkflowException("Only approved drafts can be published.");
        }

        var generatedVideoUrl = NormalizeOptionalText(draft.GeneratedVideoUrl);
        var generatedThumbnailUrl = NormalizeOptionalText(draft.GeneratedThumbnailUrl);
        if (generatedVideoUrl is null && generatedThumbnailUrl is null)
        {
            throw new ExerciseMediaDraftWorkflowException("Draft has no generated media to publish.");
        }

        var exercise = draft.ExerciseCatalogItem;
        if (exercise is null)
        {
            throw new ExerciseMediaDraftWorkflowException("The draft exercise is no longer available.");
        }

        if (IsSourceSnapshotStale(draft.SourceSnapshotJson, exercise))
        {
            throw new ExerciseMediaDraftWorkflowException("Draft source snapshot is stale. Create a new draft before publishing.");
        }

        ExerciseMediaPublication publication;
        try
        {
            publication = await _storageService.PrepareDraftPublicationAsync(
                exercise.Id,
                draft.Id,
                generatedVideoUrl,
                generatedThumbnailUrl,
                cancellationToken);
        }
        catch (ExerciseMediaStorageException exception)
        {
            throw new ExerciseMediaDraftWorkflowException(exception.Message);
        }

        using (publication)
        await using (var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            var now = DateTime.UtcNow;
            if (publication.VideoPublicUrl is not null)
            {
                exercise.LocalVideoUrlOverride = publication.VideoPublicUrl;
            }

            if (publication.ThumbnailPublicUrl is not null)
            {
                exercise.LocalThumbnailUrlOverride = publication.ThumbnailPublicUrl;
            }

            exercise.IsManuallyEdited = HasManualOverrides(exercise);
            exercise.LastEditedAt = exercise.IsManuallyEdited ? now : null;
            exercise.UpdatedAt = now;

            draft.Status = ExerciseMediaDraftStatuses.Published;
            draft.PublishedByUserId = publishedByUserId;
            draft.PublishedAt = now;
            draft.UpdatedAt = now;

            await SaveDraftChangesAsync(cancellationToken);
            try
            {
                publication.CommitFiles();
            }
            catch (ExerciseMediaStorageException exception)
            {
                throw new ExerciseMediaDraftWorkflowException(exception.Message);
            }

            await transaction.CommitAsync(cancellationToken);
            publication.Complete();
        }

        return MapDraft(draft);
    }

    public async Task<ExerciseMediaDraftResponse?> StartGenerationAsync(
        int draftId,
        CancellationToken cancellationToken = default)
    {
        var draft = await GetDraftForUpdateAsync(draftId, cancellationToken);
        if (draft is null)
        {
            return null;
        }

        if (!CanStartGeneration(draft.Status))
        {
            throw new ExerciseMediaDraftWorkflowException(
                "Only queued, review, failed, or rejected drafts can start generation.");
        }

        if (!string.Equals(draft.MediaType, ExerciseMediaDraftMediaTypes.Video, StringComparison.Ordinal))
        {
            throw new ExerciseMediaDraftWorkflowException(
                "Only video drafts can use the configured video generation provider.");
        }

        if (string.IsNullOrWhiteSpace(draft.PromptText))
        {
            throw new ExerciseMediaDraftWorkflowException("Draft prompt text is required for generation.");
        }

        var provider = GetGenerationProvider(_generationOptions.Provider);
        try
        {
            provider.ValidateConfiguration();
        }
        catch (ExerciseMediaGenerationException exception)
        {
            throw new ExerciseMediaDraftWorkflowException(exception.Message);
        }

        var originalStatus = draft.Status;
        var claimTime = DateTime.UtcNow;
        var claimed = await _dbContext.ExerciseMediaDrafts
            .Where(candidate =>
                candidate.Id == draft.Id &&
                (candidate.Status == ExerciseMediaDraftStatuses.Queued ||
                 candidate.Status == ExerciseMediaDraftStatuses.NeedsReview ||
                 candidate.Status == ExerciseMediaDraftStatuses.Failed ||
                 candidate.Status == ExerciseMediaDraftStatuses.Rejected))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(candidate => candidate.Status, ExerciseMediaDraftStatuses.Generating)
                    .SetProperty(candidate => candidate.UpdatedAt, claimTime),
                cancellationToken);

        if (claimed != 1)
        {
            throw new ExerciseMediaDraftWorkflowException(
                "Draft status changed before generation could start. Refresh and try again.");
        }

        draft.Status = ExerciseMediaDraftStatuses.Generating;
        draft.UpdatedAt = claimTime;
        _dbContext.Entry(draft).Property(candidate => candidate.UpdatedAt).OriginalValue = claimTime;

        ExerciseMediaGenerationStartResult result;
        try
        {
            result = await provider.StartGenerationAsync(draft, cancellationToken);
        }
        catch (ExerciseMediaGenerationException exception)
        {
            draft.Status = originalStatus;
            draft.ErrorMessage = exception.Message;
            draft.UpdatedAt = DateTime.UtcNow;
            await SaveDraftChangesAsync(cancellationToken);
            throw new ExerciseMediaDraftWorkflowException(exception.Message);
        }

        var now = DateTime.UtcNow;
        draft.Status = ExerciseMediaDraftStatuses.Generating;
        draft.GenerationProvider = result.Provider;
        draft.GenerationModel = result.Model;
        draft.ProviderJobId = result.ProviderJobId;
        draft.GeneratedVideoUrl = null;
        draft.ErrorMessage = null;
        draft.GeneratedAt = null;
        draft.ReviewNotes = null;
        draft.RejectionReason = null;
        draft.ReviewedByUserId = null;
        draft.ReviewedAt = null;
        draft.UpdatedAt = now;

        await SaveDraftChangesAsync(cancellationToken);
        return MapDraft(draft);
    }

    public async Task<ExerciseMediaDraftResponse?> RefreshGenerationStatusAsync(
        int draftId,
        CancellationToken cancellationToken = default)
    {
        var draft = await GetDraftForUpdateAsync(draftId, cancellationToken);
        if (draft is null)
        {
            return null;
        }

        if (!string.Equals(draft.Status, ExerciseMediaDraftStatuses.Generating, StringComparison.Ordinal))
        {
            throw new ExerciseMediaDraftWorkflowException(
                "Only generating drafts can refresh provider status.");
        }

        if (string.IsNullOrWhiteSpace(draft.ProviderJobId))
        {
            throw new ExerciseMediaDraftWorkflowException(
                "The draft does not have a provider job identifier.");
        }

        var provider = GetGenerationProvider(draft.GenerationProvider);
        ExerciseMediaGenerationRefreshResult result;
        try
        {
            result = await provider.RefreshGenerationStatusAsync(draft, cancellationToken);
        }
        catch (ExerciseMediaGenerationException exception)
        {
            throw new ExerciseMediaDraftWorkflowException(exception.Message);
        }

        var now = DateTime.UtcNow;
        switch (result.State)
        {
            case ExerciseMediaGenerationState.Pending:
                draft.Status = ExerciseMediaDraftStatuses.Generating;
                break;

            case ExerciseMediaGenerationState.Failed:
                draft.Status = ExerciseMediaDraftStatuses.Failed;
                draft.ErrorMessage = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "Video generation failed."
                    : result.ErrorMessage;
                draft.GeneratedAt = null;
                break;

            case ExerciseMediaGenerationState.Completed:
                if (string.IsNullOrWhiteSpace(result.GeneratedVideoUrl))
                {
                    throw new ExerciseMediaDraftWorkflowException(
                        "The video provider completed without stored media.");
                }

                draft.GeneratedVideoUrl = result.GeneratedVideoUrl;
                draft.Status = ExerciseMediaDraftStatuses.NeedsReview;
                draft.ErrorMessage = null;
                draft.GeneratedAt = now;
                break;

            default:
                throw new ExerciseMediaDraftWorkflowException(
                    "The video provider returned an unsupported generation state.");
        }

        draft.UpdatedAt = now;
        await SaveDraftChangesAsync(cancellationToken);
        return MapDraft(draft);
    }

    private async Task<ExerciseMediaDraft?> GetDraftForUpdateAsync(int draftId, CancellationToken cancellationToken)
    {
        return await _dbContext.ExerciseMediaDrafts
            .Include(draft => draft.ExerciseCatalogItem)
            .FirstOrDefaultAsync(draft => draft.Id == draftId, cancellationToken);
    }

    private IExerciseMediaGenerationProvider GetGenerationProvider(string? providerName)
    {
        if (!_generationOptions.Enabled)
        {
            throw new ExerciseMediaDraftWorkflowException("Media generation is disabled.");
        }

        var normalizedProviderName = providerName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedProviderName))
        {
            throw new ExerciseMediaDraftWorkflowException(
                "A media generation provider is not configured for this draft.");
        }

        var provider = _generationProviders.FirstOrDefault(candidate =>
            string.Equals(candidate.ProviderName, normalizedProviderName, StringComparison.OrdinalIgnoreCase));

        return provider ?? throw new ExerciseMediaDraftWorkflowException(
            "The configured media generation provider is not available.");
    }

    private static bool CanStartGeneration(string status)
    {
        return string.Equals(status, ExerciseMediaDraftStatuses.Queued, StringComparison.Ordinal) ||
            string.Equals(status, ExerciseMediaDraftStatuses.NeedsReview, StringComparison.Ordinal) ||
            string.Equals(status, ExerciseMediaDraftStatuses.Failed, StringComparison.Ordinal) ||
            string.Equals(status, ExerciseMediaDraftStatuses.Rejected, StringComparison.Ordinal);
    }

    private async Task SaveDraftChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ExerciseMediaDraftWorkflowException(
                "Draft status changed while this action was running. Refresh and try again.");
        }
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

    private static bool IsSourceSnapshotStale(string? sourceSnapshotJson, ExerciseCatalogItem exercise)
    {
        if (string.IsNullOrWhiteSpace(sourceSnapshotJson))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(sourceSnapshotJson);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !TryGetSnapshotExerciseId(root, out var snapshotExerciseId) ||
                !TryGetSnapshotString(root, "name", out var snapshotName) ||
                !TryGetSnapshotString(root, "source", out var snapshotSource) ||
                !TryGetSnapshotString(root, "externalId", out var snapshotExternalId))
            {
                return true;
            }

            return snapshotExerciseId != exercise.Id ||
                !string.Equals(snapshotName, exercise.Name, StringComparison.Ordinal) ||
                !string.Equals(snapshotSource, exercise.Source, StringComparison.Ordinal) ||
                !string.Equals(snapshotExternalId, exercise.ExternalId, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private static bool TryGetSnapshotExerciseId(JsonElement root, out int exerciseId)
    {
        exerciseId = 0;
        return root.TryGetProperty("exerciseId", out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out exerciseId);
    }

    private static bool TryGetSnapshotString(JsonElement root, string propertyName, out string? value)
    {
        value = null;
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return true;
    }

    private static bool HasManualOverrides(ExerciseCatalogItem item)
    {
        return !string.IsNullOrWhiteSpace(item.LocalNameOverride)
            || !string.IsNullOrWhiteSpace(item.LocalInstructionsOverride)
            || !string.IsNullOrWhiteSpace(item.LocalThumbnailUrlOverride)
            || !string.IsNullOrWhiteSpace(item.LocalVideoUrlOverride);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
