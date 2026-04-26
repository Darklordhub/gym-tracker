namespace backend.Contracts;

public class ExerciseCatalogMediaEnrichmentResponse
{
    public int Checked { get; set; }
    public int Updated { get; set; }
    public int ImagesAdded { get; set; }
    public int VideosAdded { get; set; }
    public Dictionary<string, int> SourceBreakdown { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int SkippedAmbiguous { get; set; }
    public int StillMissingImages { get; set; }
    public int StillMissingVideos { get; set; }
    public int Failed { get; set; }
    public string MediaType { get; set; } = "all";
    public string Provider { get; set; } = "all";
    public int Limit { get; set; }
}
