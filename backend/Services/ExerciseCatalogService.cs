using backend.Contracts;
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public class ExerciseCatalogService
{
    public const int MaxPageSize = 50;

    private readonly AppDbContext _dbContext;

    public ExerciseCatalogService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ExerciseCatalogItemResponse>> GetAllAsync()
    {
        var items = await BuildBaseQuery()
            .OrderBy(item => item.Name)
            .ToListAsync();

        return items.Select(MapItem).ToList();
    }

    public async Task<IReadOnlyList<ExerciseCatalogItemResponse>> SearchAsync(string? query)
    {
        var normalizedQuery = query?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return await GetAllAsync();
        }

        var items = await ApplySearchFilter(BuildBaseQuery(), normalizedQuery)
            .OrderBy(item => item.Name)
            .ToListAsync();

        return items.Select(MapItem).ToList();
    }

    public async Task<ExerciseCatalogPageResponse> GetPageAsync(int page, int pageSize, string? query = null)
    {
        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var normalizedQuery = query?.Trim();

        var filteredQuery = string.IsNullOrWhiteSpace(normalizedQuery)
            ? BuildBaseQuery()
            : ApplySearchFilter(BuildBaseQuery(), normalizedQuery);

        var totalCount = await filteredQuery.CountAsync();
        var items = await filteredQuery
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync();

        return new ExerciseCatalogPageResponse
        {
            Items = items.Select(MapItem).ToList(),
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<ExerciseCatalogItemResponse?> GetByIdAsync(int id)
    {
        var item = await BuildBaseQuery()
            .FirstOrDefaultAsync(entry => entry.Id == id);

        return item is null ? null : MapItem(item);
    }

    public async Task<IReadOnlyList<AdminExerciseCatalogItemResponse>> GetAdminItemsAsync(string? query)
    {
        var normalizedQuery = query?.Trim();
        IQueryable<ExerciseCatalogItem> catalogQuery = _dbContext.ExerciseCatalogItems.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            var pattern = $"%{normalizedQuery}%";
            catalogQuery = catalogQuery.Where(item =>
                EF.Functions.ILike(item.LocalNameOverride ?? item.Name, pattern)
                || EF.Functions.ILike(item.Name, pattern)
                || (item.PrimaryMuscle != null && EF.Functions.ILike(item.PrimaryMuscle, pattern))
                || (item.Equipment != null && EF.Functions.ILike(item.Equipment, pattern))
                || EF.Functions.ILike(item.SecondaryMuscles, pattern));
        }

        var items = await catalogQuery
            .OrderBy(item => item.LocalNameOverride ?? item.Name)
            .ThenBy(item => item.Id)
            .ToListAsync();

        return items.Select(MapAdminItem).ToList();
    }

    public async Task<AdminExerciseCatalogItemResponse?> GetAdminItemByIdAsync(int id)
    {
        var item = await _dbContext.ExerciseCatalogItems
            .AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Id == id);

        return item is null ? null : MapAdminItem(item);
    }

    private IQueryable<ExerciseCatalogItem> BuildBaseQuery()
    {
        return _dbContext.ExerciseCatalogItems
            .AsNoTracking()
            .Where(item => item.IsActive);
    }

    private static IQueryable<ExerciseCatalogItem> ApplySearchFilter(IQueryable<ExerciseCatalogItem> query, string normalizedQuery)
    {
        var pattern = $"%{normalizedQuery}%";
        return query.Where(item =>
            EF.Functions.ILike(item.LocalNameOverride ?? item.Name, pattern)
            || EF.Functions.ILike(item.Name, pattern)
            || (item.PrimaryMuscle != null && EF.Functions.ILike(item.PrimaryMuscle, pattern))
            || (item.Equipment != null && EF.Functions.ILike(item.Equipment, pattern))
            || (item.Difficulty != null && EF.Functions.ILike(item.Difficulty, pattern))
            || EF.Functions.ILike(item.SecondaryMuscles, pattern));
    }

    private static ExerciseCatalogItemResponse MapItem(ExerciseCatalogItem item)
    {
        return new ExerciseCatalogItemResponse
        {
            Id = item.Id,
            Source = item.Source,
            ExternalId = item.ExternalId,
            Name = GetEffectiveName(item),
            ProviderName = item.Name,
            Slug = item.Slug,
            Description = item.Description,
            Instructions = GetEffectiveInstructions(item),
            PrimaryMuscle = item.PrimaryMuscle,
            SecondaryMuscles = SplitList(item.SecondaryMuscles),
            Equipment = item.Equipment,
            Difficulty = item.Difficulty,
            VideoUrl = GetEffectiveVideoUrl(item),
            ThumbnailUrl = GetEffectiveThumbnailUrl(item),
            LocalMediaPath = item.LocalMediaPath,
            IsActive = item.IsActive,
            LastSyncedAt = item.LastSyncedAt,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
        };
    }

    private static AdminExerciseCatalogItemResponse MapAdminItem(ExerciseCatalogItem item)
    {
        return new AdminExerciseCatalogItemResponse
        {
            Id = item.Id,
            Source = item.Source,
            ExternalId = item.ExternalId,
            Name = GetEffectiveName(item),
            ProviderName = item.Name,
            Slug = item.Slug,
            Description = item.Description,
            Instructions = GetEffectiveInstructions(item),
            ProviderInstructions = item.Instructions,
            PrimaryMuscle = item.PrimaryMuscle,
            SecondaryMuscles = SplitList(item.SecondaryMuscles),
            Equipment = item.Equipment,
            Difficulty = item.Difficulty,
            VideoUrl = GetEffectiveVideoUrl(item),
            ProviderVideoUrl = item.VideoUrl,
            ThumbnailUrl = GetEffectiveThumbnailUrl(item),
            ProviderThumbnailUrl = item.ThumbnailUrl,
            IsActive = item.IsActive,
            IsManuallyEdited = item.IsManuallyEdited,
            LastSyncedAt = item.LastSyncedAt,
            LastEditedAt = item.LastEditedAt,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
        };
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
        return string.IsNullOrWhiteSpace(item.LocalInstructionsOverride) ? item.Instructions : item.LocalInstructionsOverride.Trim();
    }

    private static string? GetEffectiveThumbnailUrl(ExerciseCatalogItem item)
    {
        return string.IsNullOrWhiteSpace(item.LocalThumbnailUrlOverride) ? item.ThumbnailUrl : item.LocalThumbnailUrlOverride.Trim();
    }

    private static string? GetEffectiveVideoUrl(ExerciseCatalogItem item)
    {
        return string.IsNullOrWhiteSpace(item.LocalVideoUrlOverride) ? item.VideoUrl : item.LocalVideoUrlOverride.Trim();
    }
}
