using backend.Dtos;

namespace backend.Services;

public interface IAiWorkoutGeneratorService
{
    Task<AiWorkoutPlanDto> GenerateAsync(
        string userId,
        AiWorkoutGenerateRequest request,
        CancellationToken cancellationToken);
}
