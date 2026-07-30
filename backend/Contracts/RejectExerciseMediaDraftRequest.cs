using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public class RejectExerciseMediaDraftRequest
{
    [MaxLength(4000)]
    public string? ReviewNotes { get; set; }

    [MaxLength(2000)]
    public string? RejectionReason { get; set; }
}
