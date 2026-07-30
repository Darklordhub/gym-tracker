using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public class ReviewExerciseMediaDraftRequest
{
    [MaxLength(4000)]
    public string? ReviewNotes { get; set; }
}
