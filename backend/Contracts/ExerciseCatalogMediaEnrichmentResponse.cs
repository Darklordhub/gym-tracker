namespace backend.Contracts;

public class ExerciseCatalogMediaEnrichmentResponse
{
    public int Checked { get; set; }
    public int Updated { get; set; }
    public int ImagesAdded { get; set; }
    public int VideosAdded { get; set; }
    public int BadUrlsRepaired { get; set; }
    public Dictionary<string, int> SourceBreakdown { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int SkippedAmbiguous { get; set; }
    public int SkippedLowConfidence { get; set; }
    public int StillMissingImages { get; set; }
    public int StillMissingVideos { get; set; }
    public int Failed { get; set; }
    public string MediaType { get; set; } = "all";
    public string Provider { get; set; } = "all";
    public int Limit { get; set; }
    public bool DryRun { get; set; }
    public bool RepairBadUrls { get; set; }
    public List<ExerciseCatalogMediaEnrichmentSample> SampleUpdated { get; set; } = [];
    public List<ExerciseCatalogMediaStillMissingSample> SampleStillMissing { get; set; } = [];
    public List<ExerciseCatalogMediaBrokenUrlSample> SampleBrokenUnrepaired { get; set; } = [];
}

public class ExerciseCatalogMediaEnrichmentSample
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string ProviderUsed { get; set; } = string.Empty;
    public string? MatchedExerciseName { get; set; }
    public double Confidence { get; set; }
    public string? ImageAction { get; set; }
    public string? VideoAction { get; set; }
    public string? Message { get; set; }
}

public class ExerciseCatalogMediaStillMissingSample
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public string? PrimaryMuscle { get; set; }
    public string? Equipment { get; set; }
    public string ReasonMissing { get; set; } = string.Empty;
}

public class ExerciseCatalogMediaBrokenUrlSample
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MediaField { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int? StatusCode { get; set; }
    public string? Error { get; set; }
}
