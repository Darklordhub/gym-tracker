namespace backend.Models;

public static class ExerciseMediaDraftStatuses
{
    public const string Queued = "Queued";
    public const string Generating = "Generating";
    public const string Generated = "Generated";
    public const string NeedsReview = "NeedsReview";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Published = "Published";
    public const string Failed = "Failed";
    public const string Archived = "Archived";
}
