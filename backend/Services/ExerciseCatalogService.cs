using backend.Contracts;
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public class ExerciseCatalogService
{
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

        var pattern = $"%{normalizedQuery}%";
        var items = await BuildBaseQuery()
            .Where(item =>
                EF.Functions.ILike(item.Name, pattern)
                || (item.PrimaryMuscle != null && EF.Functions.ILike(item.PrimaryMuscle, pattern))
                || (item.Equipment != null && EF.Functions.ILike(item.Equipment, pattern))
                || (item.Difficulty != null && EF.Functions.ILike(item.Difficulty, pattern))
                || EF.Functions.ILike(item.SecondaryMuscles, pattern))
            .OrderBy(item => item.Name)
            .ToListAsync();

        return items.Select(MapItem).ToList();
    }

    public async Task<ExerciseCatalogItemResponse?> GetByIdAsync(int id)
    {
        var item = await BuildBaseQuery()
            .FirstOrDefaultAsync(entry => entry.Id == id);

        return item is null ? null : MapItem(item);
    }

    private IQueryable<ExerciseCatalogItem> BuildBaseQuery()
    {
        return _dbContext.ExerciseCatalogItems
            .AsNoTracking()
            .Where(item => item.IsActive);
    }

    private static ExerciseCatalogItemResponse MapItem(ExerciseCatalogItem item)
    {
        return new ExerciseCatalogItemResponse
        {
            Id = item.Id,
            Source = item.Source,
            ExternalId = item.ExternalId,
            Name = item.Name,
            Slug = item.Slug,
            Description = item.Description,
            Instructions = item.Instructions,
            PrimaryMuscle = item.PrimaryMuscle,
            SecondaryMuscles = SplitList(item.SecondaryMuscles),
            Equipment = item.Equipment,
            Difficulty = item.Difficulty,
            VideoUrl = item.VideoUrl,
            ThumbnailUrl = item.ThumbnailUrl,
            LocalMediaPath = item.LocalMediaPath,
            IsActive = item.IsActive,
            LastSyncedAt = item.LastSyncedAt,
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
}
