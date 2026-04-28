using backend.Models;

namespace backend.Services;

public interface IExerciseMediaProvider
{
    string Name { get; }
    int Priority { get; }
    bool IsEnabled { get; }

    Task<ExerciseMediaMatchResult> TryGetMediaAsync(
        ExerciseCatalogItem item,
        ExerciseMediaRequirement requirement,
        CancellationToken cancellationToken = default);
}

public sealed class ExerciseMediaRequirement
{
    public bool NeedImage { get; init; }
    public bool NeedVideo { get; init; }
}

public enum ExerciseMediaMatchStatus
{
    NotFound,
    Found,
    Ambiguous,
    LowConfidence,
    Failed,
    Skipped,
}

public sealed class ExerciseMediaMatchResult
{
    public ExerciseMediaMatchStatus Status { get; init; }
    public string? ImageUrl { get; init; }
    public string? VideoUrl { get; init; }
    public double Confidence { get; init; }
    public string? Message { get; init; }
    public string? MatchedName { get; init; }

    public static ExerciseMediaMatchResult Skipped(string? message = null) => new()
    {
        Status = ExerciseMediaMatchStatus.Skipped,
        Message = message,
    };

    public static ExerciseMediaMatchResult NotFound(string? message = null) => new()
    {
        Status = ExerciseMediaMatchStatus.NotFound,
        Message = message,
    };

    public static ExerciseMediaMatchResult Ambiguous(string? message = null, double confidence = 0) => new()
    {
        Status = ExerciseMediaMatchStatus.Ambiguous,
        Message = message,
        Confidence = confidence,
    };

    public static ExerciseMediaMatchResult LowConfidence(string? message = null, double confidence = 0, string? matchedName = null) => new()
    {
        Status = ExerciseMediaMatchStatus.LowConfidence,
        Message = message,
        Confidence = confidence,
        MatchedName = matchedName,
    };

    public static ExerciseMediaMatchResult Failed(string? message = null) => new()
    {
        Status = ExerciseMediaMatchStatus.Failed,
        Message = message,
    };

    public static ExerciseMediaMatchResult Found(
        string? imageUrl,
        string? videoUrl,
        double confidence,
        string? message = null,
        string? matchedName = null) => new()
    {
        Status = ExerciseMediaMatchStatus.Found,
        ImageUrl = imageUrl,
        VideoUrl = videoUrl,
        Confidence = confidence,
        Message = message,
        MatchedName = matchedName,
    };
}
