using System.ComponentModel.DataAnnotations;

namespace backend.Dtos;

public class AiWorkoutGenerateRequest
{
    [Required]
    [MaxLength(80)]
    public string Goal { get; set; } = string.Empty;

    [MaxLength(60)]
    public string? PreferredWorkoutType { get; set; }

    [Range(15, 180)]
    public int? DurationMinutes { get; set; }

    [MaxLength(40)]
    public string? FitnessLevel { get; set; }

    public string[]? TargetMuscles { get; set; }

    public string[]? ExcludedExercises { get; set; }

    public bool IncludeWarmup { get; set; } = true;

    public bool IncludeCooldown { get; set; } = true;
}

public class AiWorkoutPlanDto
{
    public string Title { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;
    public string WorkoutType { get; set; } = string.Empty;
    public int EstimatedDurationMinutes { get; set; }
    public string Difficulty { get; set; } = string.Empty;
    public List<AiWorkoutSectionDto> Sections { get; set; } = [];
    public List<string> Notes { get; set; } = [];
}

public class AiWorkoutSectionDto
{
    public string Name { get; set; } = string.Empty;
    public List<AiWorkoutExerciseDto> Exercises { get; set; } = [];
}

public class AiWorkoutExerciseDto
{
    public int? ExerciseCatalogItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? TargetMuscle { get; set; }
    public int Sets { get; set; }
    public string Reps { get; set; } = string.Empty;
    public List<AiWorkoutSetDto> SuggestedSets { get; set; } = [];
    public string? SuggestedWeight { get; set; }
    public int RestSeconds { get; set; }
    public string Instructions { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? VideoUrl { get; set; }
}

public class AiWorkoutSetDto
{
    public int Reps { get; set; }
    public double WeightKg { get; set; }
}
