namespace backend.Configuration;

public class WgerOptions
{
    public const string SectionName = "Wger";

    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "https://wger.de/api/v2/";
    public int? LanguageId { get; set; } = 2;
    public string? LanguageCode { get; set; }
    public int PageSize { get; set; } = 50;
    public int MaxItems { get; set; } = 1000;
}
