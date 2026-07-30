using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public class CreateExerciseMediaDraftRequest
{
    [MaxLength(20)]
    public string? MediaType { get; set; }
}
