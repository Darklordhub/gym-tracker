namespace backend.Contracts;

public class ExerciseCatalogMediaDiagnosticsResponse
{
    public string SourceFilter { get; set; } = "all";
    public bool UrlValidationRun { get; set; }
    public int SampleSize { get; set; }
    public int TotalCatalogItems { get; set; }
    public int ActiveItems { get; set; }
    public int ItemsWithEffectiveThumbnail { get; set; }
    public int ItemsMissingEffectiveThumbnail { get; set; }
    public int ItemsWithProviderThumbnail { get; set; }
    public int ItemsWithLocalThumbnailUrlOverride { get; set; }
    public int ItemsWithVideoUrl { get; set; }
    public int ItemsWithLocalVideoUrlOverride { get; set; }
    public int BrokenThumbnailUrlCount { get; set; }
    public List<ExerciseCatalogMediaStillMissingSample> SampleMissingItems { get; set; } = [];
    public List<ExerciseCatalogMediaBrokenUrlSample> SampleBrokenUrls { get; set; } = [];
}
