namespace backend.Services;

public class NutritionProviderException : Exception
{
    public NutritionProviderException(string message)
        : base(message)
    {
    }

    public NutritionProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
