namespace backend.Services;

public class NutritionModeConflictException : Exception
{
    public const string ManualCalorieEntryMessage =
        "This day already has a manual calorie entry. Switch to meal tracking to replace it.";

    public NutritionModeConflictException()
        : base(ManualCalorieEntryMessage)
    {
    }

    public NutritionModeConflictException(string message)
        : base(message)
    {
    }

    public NutritionModeConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
