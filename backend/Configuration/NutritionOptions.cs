namespace backend.Configuration;

public class NutritionOptions
{
    public const string SectionName = "Nutrition";

    public string? UsdaApiKey { get; set; }
    public string UsdaBaseUrl { get; set; } = "https://api.nal.usda.gov/fdc/v1/";
    public int SearchCacheMinutes { get; set; } = 10;
    public int DetailCacheMinutes { get; set; } = 60;
}
