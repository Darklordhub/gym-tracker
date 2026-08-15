namespace backend.Services;

public static class AiWorkoutProviderFailureCategories
{
    public const string ProviderFailure = "ProviderFailure";
    public const string OpenAiHttpFailure = "OpenAiHttpFailure";
    public const string OpenAiEmptyOutput = "OpenAiEmptyOutput";
    public const string OpenAiJsonParseFailure = "OpenAiJsonParseFailure";
    public const string OpenAiSchemaMismatch = "OpenAiSchemaMismatch";
    public const string OpenAiNoSections = "OpenAiNoSections";
    public const string OpenAiNoExercises = "OpenAiNoExercises";
    public const string OpenAiValidationFailure = "OpenAiValidationFailure";
    public const string OpenAiUnknownExerciseId = "OpenAiUnknownExerciseId";
    public const string OpenAiDuplicateExerciseId = "OpenAiDuplicateExerciseId";
    public const string OpenAiInactiveExerciseId = "OpenAiInactiveExerciseId";
    public const string OpenAiInvalidSets = "OpenAiInvalidSets";
    public const string OpenAiInvalidRest = "OpenAiInvalidRest";
    public const string OpenAiInvalidReps = "OpenAiInvalidReps";
    public const string OpenAiDurationExerciseCapExceeded = "OpenAiDurationExerciseCapExceeded";

    private static readonly HashSet<string> KnownCategories =
    [
        ProviderFailure,
        OpenAiHttpFailure,
        OpenAiEmptyOutput,
        OpenAiJsonParseFailure,
        OpenAiSchemaMismatch,
        OpenAiNoSections,
        OpenAiNoExercises,
        OpenAiValidationFailure,
        OpenAiUnknownExerciseId,
        OpenAiDuplicateExerciseId,
        OpenAiInactiveExerciseId,
        OpenAiInvalidSets,
        OpenAiInvalidRest,
        OpenAiInvalidReps,
        OpenAiDurationExerciseCapExceeded,
    ];

    public static string Normalize(string? errorCategory) =>
        errorCategory is not null && KnownCategories.Contains(errorCategory)
            ? errorCategory
            : ProviderFailure;
}
