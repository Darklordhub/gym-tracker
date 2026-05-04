using backend.Contracts;

namespace backend.Services;

public interface IMealService
{
    Task<DailyMealsDto> GetMealsForDateAsync(int userId, DateOnly date, CancellationToken cancellationToken);

    Task<DailyMealsDto> SwitchDayToMealsAsync(int userId, DateOnly date, CancellationToken cancellationToken);

    Task<UserMealDto?> GetMealByIdAsync(int userId, int mealId, CancellationToken cancellationToken);

    Task<UserMealDto> CreateMealAsync(int userId, DateOnly date, CreateMealRequest request, CancellationToken cancellationToken);

    Task<UserMealDto?> UpdateMealAsync(int userId, int mealId, UpdateMealRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteMealAsync(int userId, int mealId, CancellationToken cancellationToken);

    Task<UserMealItemDto?> AddMealItemAsync(int userId, int mealId, AddMealItemRequest request, CancellationToken cancellationToken);

    Task<UserMealItemDto?> UpdateMealItemAsync(int userId, int mealItemId, UpdateMealItemRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteMealItemAsync(int userId, int mealItemId, CancellationToken cancellationToken);
}
