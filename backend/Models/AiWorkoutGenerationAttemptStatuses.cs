namespace backend.Models;

public static class AiWorkoutGenerationAttemptStatuses
{
    public const string Reserved = "Reserved";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string FallbackSucceeded = "FallbackSucceeded";
    public const string RateLimited = "RateLimited";
}
