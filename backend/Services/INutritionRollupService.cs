namespace backend.Services;

public interface INutritionRollupService
{
    Task RollupDayAsync(int userId, DateOnly date, CancellationToken cancellationToken);

    Task SwitchDayToMealsAsync(int userId, DateOnly date, CancellationToken cancellationToken);
}
