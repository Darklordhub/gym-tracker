using backend.Contracts;
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public class MealService : IMealService
{
    private readonly AppDbContext _dbContext;
    private readonly INutritionService _nutritionService;

    public MealService(AppDbContext dbContext, INutritionService nutritionService)
    {
        _dbContext = dbContext;
        _nutritionService = nutritionService;
    }

    public async Task<DailyMealsDto> GetMealsForDateAsync(int userId, DateOnly date, CancellationToken cancellationToken)
    {
        var meals = await _dbContext.UserMeals
            .AsNoTracking()
            .Where(meal => meal.UserId == userId && meal.Date == date)
            .Include(meal => meal.Items)
            .OrderBy(meal => meal.MealType)
            .ThenBy(meal => meal.Id)
            .ToListAsync(cancellationToken);

        return new DailyMealsDto
        {
            Date = date,
            Meals = meals.Select(MapMeal).ToList(),
            TotalCalories = Round(meals.Sum(meal => meal.TotalCalories)),
            TotalProtein = Round(meals.Sum(meal => meal.TotalProtein)),
            TotalCarbs = Round(meals.Sum(meal => meal.TotalCarbs)),
            TotalFat = Round(meals.Sum(meal => meal.TotalFat)),
            TotalFiber = SumNullable(meals.Select(meal => meal.TotalFiber)),
            TotalSugar = SumNullable(meals.Select(meal => meal.TotalSugar)),
        };
    }

    public async Task<UserMealDto?> GetMealByIdAsync(int userId, int mealId, CancellationToken cancellationToken)
    {
        var meal = await _dbContext.UserMeals
            .AsNoTracking()
            .Where(meal => meal.Id == mealId && meal.UserId == userId)
            .Include(meal => meal.Items)
            .FirstOrDefaultAsync(cancellationToken);

        return meal is null ? null : MapMeal(meal);
    }

    public async Task<UserMealDto> CreateMealAsync(int userId, DateOnly date, CreateMealRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = DateTime.UtcNow;
        var meal = new UserMeal
        {
            UserId = userId,
            Date = date,
            MealType = NormalizeMealType(request.MealType),
            Title = NormalizeOptionalText(request.Title),
            Notes = NormalizeOptionalText(request.Notes),
            TotalCalories = 0m,
            TotalProtein = 0m,
            TotalCarbs = 0m,
            TotalFat = 0m,
            TotalFiber = null,
            TotalSugar = null,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _dbContext.UserMeals.Add(meal);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetMealByIdAsync(userId, meal.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Created meal '{meal.Id}' could not be reloaded.");
    }

    public async Task<UserMealDto?> UpdateMealAsync(int userId, int mealId, UpdateMealRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var meal = await _dbContext.UserMeals
            .Include(currentMeal => currentMeal.Items)
            .FirstOrDefaultAsync(currentMeal => currentMeal.Id == mealId && currentMeal.UserId == userId, cancellationToken);

        if (meal is null)
        {
            return null;
        }

        meal.Date = request.Date;
        meal.MealType = NormalizeMealType(request.MealType);
        meal.Title = NormalizeOptionalText(request.Title);
        meal.Notes = NormalizeOptionalText(request.Notes);
        meal.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapMeal(meal);
    }

    public async Task<bool> DeleteMealAsync(int userId, int mealId, CancellationToken cancellationToken)
    {
        var meal = await _dbContext.UserMeals
            .FirstOrDefaultAsync(currentMeal => currentMeal.Id == mealId && currentMeal.UserId == userId, cancellationToken);

        if (meal is null)
        {
            return false;
        }

        _dbContext.UserMeals.Remove(meal);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<UserMealItemDto?> AddMealItemAsync(int userId, int mealId, AddMealItemRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var meal = await _dbContext.UserMeals
            .Include(currentMeal => currentMeal.Items)
            .FirstOrDefaultAsync(currentMeal => currentMeal.Id == mealId && currentMeal.UserId == userId, cancellationToken);

        if (meal is null)
        {
            return null;
        }

        var sourceProvider = NormalizeRequiredText(request.SourceProvider, nameof(request.SourceProvider), "Source provider is required.");
        var externalFoodId = NormalizeRequiredText(request.ExternalFoodId, nameof(request.ExternalFoodId), "External food ID is required.");
        var normalizedUnit = NormalizeGramUnit(request.Unit);
        var nutritionDetail = await _nutritionService.GetFoodDetailAsync(sourceProvider, externalFoodId, cancellationToken);

        if (nutritionDetail is null)
        {
            throw new ArgumentException(
                $"Food '{externalFoodId}' was not found for source '{sourceProvider}'.",
                nameof(request));
        }

        if (!nutritionDetail.SupportedUnits.Contains("g", StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Food '{nutritionDetail.Name}' does not support gram-based meal logging yet.",
                nameof(request));
        }

        var catalogItem = await _dbContext.NutritionCatalogItems
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Source == sourceProvider && item.ExternalId == externalFoodId,
                cancellationToken);
        var grams = Round(request.Quantity);
        var now = DateTime.UtcNow;
        var mealItem = new UserMealItem
        {
            UserMealId = meal.Id,
            NutritionCatalogItemId = catalogItem?.Id,
            FoodNameSnapshot = nutritionDetail.Name,
            BrandNameSnapshot = NormalizeOptionalText(nutritionDetail.BrandName),
            SourceProvider = sourceProvider,
            ExternalFoodId = externalFoodId,
            Quantity = grams,
            Unit = normalizedUnit,
            ConsumedGrams = grams,
            Calories = Scale(nutritionDetail.CaloriesPer100Grams, grams),
            Protein = Scale(nutritionDetail.ProteinGramsPer100Grams, grams),
            Carbs = Scale(nutritionDetail.CarbsGramsPer100Grams, grams),
            Fat = Scale(nutritionDetail.FatGramsPer100Grams, grams),
            Fiber = ScaleNullable(nutritionDetail.FiberGramsPer100Grams, grams),
            Sugar = ScaleNullable(nutritionDetail.SugarGramsPer100Grams, grams),
            SortOrder = ResolveSortOrder(meal.Items, request.SortOrder),
            CreatedAt = now,
            UpdatedAt = now,
        };

        meal.Items.Add(mealItem);
        RecalculateMealTotals(meal);
        meal.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapMealItem(mealItem);
    }

    public async Task<UserMealItemDto?> UpdateMealItemAsync(int userId, int mealItemId, UpdateMealItemRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var mealItem = await _dbContext.UserMealItems
            .Include(item => item.UserMeal)
            .ThenInclude(meal => meal!.Items)
            .FirstOrDefaultAsync(
                item => item.Id == mealItemId
                    && item.UserMeal != null
                    && item.UserMeal.UserId == userId,
                cancellationToken);

        if (mealItem is null || mealItem.UserMeal is null)
        {
            return null;
        }

        var normalizedUnit = NormalizeGramUnit(request.Unit);
        var grams = Round(request.Quantity);
        var nutrientSnapshot = await ResolveUpdateSnapshotAsync(mealItem, cancellationToken);
        var now = DateTime.UtcNow;

        mealItem.Quantity = grams;
        mealItem.Unit = normalizedUnit;
        mealItem.ConsumedGrams = grams;
        mealItem.Calories = Scale(nutrientSnapshot.CaloriesPer100Grams, grams);
        mealItem.Protein = Scale(nutrientSnapshot.ProteinPer100Grams, grams);
        mealItem.Carbs = Scale(nutrientSnapshot.CarbsPer100Grams, grams);
        mealItem.Fat = Scale(nutrientSnapshot.FatPer100Grams, grams);
        mealItem.Fiber = ScaleNullable(nutrientSnapshot.FiberPer100Grams, grams);
        mealItem.Sugar = ScaleNullable(nutrientSnapshot.SugarPer100Grams, grams);
        mealItem.SortOrder = request.SortOrder.HasValue
            ? Math.Max(request.SortOrder.Value, 0)
            : mealItem.SortOrder;
        mealItem.UpdatedAt = now;

        RecalculateMealTotals(mealItem.UserMeal);
        mealItem.UserMeal.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapMealItem(mealItem);
    }

    public async Task<bool> DeleteMealItemAsync(int userId, int mealItemId, CancellationToken cancellationToken)
    {
        var mealItem = await _dbContext.UserMealItems
            .Include(item => item.UserMeal)
            .ThenInclude(meal => meal!.Items)
            .FirstOrDefaultAsync(
                item => item.Id == mealItemId
                    && item.UserMeal != null
                    && item.UserMeal.UserId == userId,
                cancellationToken);

        if (mealItem is null || mealItem.UserMeal is null)
        {
            return false;
        }

        var meal = mealItem.UserMeal;
        meal.Items.Remove(mealItem);
        _dbContext.UserMealItems.Remove(mealItem);
        RecalculateMealTotals(meal);
        meal.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<MealItemSnapshot> ResolveUpdateSnapshotAsync(UserMealItem mealItem, CancellationToken cancellationToken)
    {
        if (mealItem.ConsumedGrams > 0)
        {
            return new MealItemSnapshot(
                CaloriesPer100Grams: Round((mealItem.Calories / mealItem.ConsumedGrams) * 100m),
                ProteinPer100Grams: Round((mealItem.Protein / mealItem.ConsumedGrams) * 100m),
                CarbsPer100Grams: Round((mealItem.Carbs / mealItem.ConsumedGrams) * 100m),
                FatPer100Grams: Round((mealItem.Fat / mealItem.ConsumedGrams) * 100m),
                FiberPer100Grams: mealItem.Fiber.HasValue ? Round((mealItem.Fiber.Value / mealItem.ConsumedGrams) * 100m) : null,
                SugarPer100Grams: mealItem.Sugar.HasValue ? Round((mealItem.Sugar.Value / mealItem.ConsumedGrams) * 100m) : null);
        }

        var detail = await _nutritionService.GetFoodDetailAsync(mealItem.SourceProvider, mealItem.ExternalFoodId, cancellationToken);

        if (detail is null)
        {
            throw new ArgumentException(
                $"Food '{mealItem.ExternalFoodId}' was not found for source '{mealItem.SourceProvider}'.",
                nameof(mealItem));
        }

        if (!detail.SupportedUnits.Contains("g", StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Food '{detail.Name}' does not support gram-based meal logging yet.",
                nameof(mealItem));
        }

        return new MealItemSnapshot(
            detail.CaloriesPer100Grams,
            detail.ProteinGramsPer100Grams,
            detail.CarbsGramsPer100Grams,
            detail.FatGramsPer100Grams,
            detail.FiberGramsPer100Grams,
            detail.SugarGramsPer100Grams);
    }

    private static void RecalculateMealTotals(UserMeal meal)
    {
        meal.TotalCalories = Round(meal.Items.Sum(item => item.Calories));
        meal.TotalProtein = Round(meal.Items.Sum(item => item.Protein));
        meal.TotalCarbs = Round(meal.Items.Sum(item => item.Carbs));
        meal.TotalFat = Round(meal.Items.Sum(item => item.Fat));
        meal.TotalFiber = SumNullable(meal.Items.Select(item => item.Fiber));
        meal.TotalSugar = SumNullable(meal.Items.Select(item => item.Sugar));
    }

    private static UserMealDto MapMeal(UserMeal meal)
    {
        return new UserMealDto
        {
            Id = meal.Id,
            Date = meal.Date,
            MealType = meal.MealType,
            Title = meal.Title,
            Notes = meal.Notes,
            TotalCalories = meal.TotalCalories,
            TotalProtein = meal.TotalProtein,
            TotalCarbs = meal.TotalCarbs,
            TotalFat = meal.TotalFat,
            TotalFiber = meal.TotalFiber,
            TotalSugar = meal.TotalSugar,
            CreatedAt = meal.CreatedAt,
            UpdatedAt = meal.UpdatedAt,
            Items = meal.Items
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Id)
                .Select(MapMealItem)
                .ToList(),
        };
    }

    private static UserMealItemDto MapMealItem(UserMealItem item)
    {
        return new UserMealItemDto
        {
            Id = item.Id,
            NutritionCatalogItemId = item.NutritionCatalogItemId,
            FoodNameSnapshot = item.FoodNameSnapshot,
            BrandNameSnapshot = item.BrandNameSnapshot,
            SourceProvider = item.SourceProvider,
            ExternalFoodId = item.ExternalFoodId,
            Quantity = item.Quantity,
            Unit = item.Unit,
            ConsumedGrams = item.ConsumedGrams,
            Calories = item.Calories,
            Protein = item.Protein,
            Carbs = item.Carbs,
            Fat = item.Fat,
            Fiber = item.Fiber,
            Sugar = item.Sugar,
            SortOrder = item.SortOrder,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
        };
    }

    private static string NormalizeMealType(string mealType)
    {
        return NormalizeRequiredText(mealType, nameof(mealType), "Meal type is required.").ToLowerInvariant();
    }

    private static string NormalizeGramUnit(string? unit)
    {
        var normalizedUnit = NormalizeRequiredText(unit ?? string.Empty, nameof(unit), "Unit is required.").ToLowerInvariant();

        return normalizedUnit switch
        {
            "g" => "g",
            "gram" => "g",
            "grams" => "g",
            _ => throw new ArgumentException(
                $"Unit '{unit}' is not supported. Meal logging currently supports gram-based quantities only.",
                nameof(unit)),
        };
    }

    private static int ResolveSortOrder(IEnumerable<UserMealItem> existingItems, int? requestedSortOrder)
    {
        return requestedSortOrder.HasValue
            ? Math.Max(requestedSortOrder.Value, 0)
            : existingItems.DefaultIfEmpty().Max(item => item?.SortOrder ?? 0) + 1;
    }

    private static string NormalizeRequiredText(string value, string parameterName, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message, parameterName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static decimal Scale(decimal? valuePer100Grams, decimal grams)
    {
        return valuePer100Grams.HasValue
            ? Round((valuePer100Grams.Value * grams) / 100m)
            : 0m;
    }

    private static decimal? ScaleNullable(decimal? valuePer100Grams, decimal grams)
    {
        return valuePer100Grams.HasValue
            ? Round((valuePer100Grams.Value * grams) / 100m)
            : null;
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal? SumNullable(IEnumerable<decimal?> values)
    {
        var materializedValues = values.Where(value => value.HasValue).Select(value => value!.Value).ToList();
        return materializedValues.Count == 0 ? null : Round(materializedValues.Sum());
    }

    private sealed record MealItemSnapshot(
        decimal? CaloriesPer100Grams,
        decimal? ProteinPer100Grams,
        decimal? CarbsPer100Grams,
        decimal? FatPer100Grams,
        decimal? FiberPer100Grams,
        decimal? SugarPer100Grams);
}
