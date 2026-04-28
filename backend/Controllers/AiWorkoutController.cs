using System.Globalization;
using backend.Dtos;
using backend.Extensions;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Authorize]
[Route("api/ai-workout")]
public class AiWorkoutController : ControllerBase
{
    private readonly IAiWorkoutGeneratorService _aiWorkoutGeneratorService;

    public AiWorkoutController(IAiWorkoutGeneratorService aiWorkoutGeneratorService)
    {
        _aiWorkoutGeneratorService = aiWorkoutGeneratorService;
    }

    [HttpPost("generate")]
    public async Task<ActionResult<AiWorkoutPlanDto>> Generate(
        AiWorkoutGenerateRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (request.DurationMinutes.HasValue && request.DurationMinutes.Value < 15)
        {
            return BadRequest(new { message = "Duration must be at least 15 minutes." });
        }

        var userId = User.GetRequiredUserId().ToString(CultureInfo.InvariantCulture);
        var plan = await _aiWorkoutGeneratorService.GenerateAsync(userId, request, cancellationToken);
        return Ok(plan);
    }
}
