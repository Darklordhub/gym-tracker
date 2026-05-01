using backend.Contracts;

namespace backend.Services;

public interface INutritionProvider
{
    string Source { get; }

    Task<IReadOnlyList<FoodSearchResultDto>> SearchFoodsAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<NutritionProviderFoodDetail?> GetFoodDetailAsync(
        string externalId,
        CancellationToken cancellationToken);
}
