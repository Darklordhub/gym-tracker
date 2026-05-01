using backend.Contracts;

namespace backend.Services;

public interface INutritionService
{
    Task<IReadOnlyList<FoodSearchResultDto>> SearchFoodsAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<FoodDetailDto?> GetFoodDetailAsync(
        string source,
        string externalId,
        CancellationToken cancellationToken);

    Task<NutritionCalculateResult> CalculateAsync(
        NutritionCalculateRequest request,
        CancellationToken cancellationToken);
}
