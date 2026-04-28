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
    private readonly ILogger<AiWorkoutController> _logger;

    public AiWorkoutController(
        IAiWorkoutGeneratorService aiWorkoutGeneratorService,
        ILogger<AiWorkoutController> logger)
    {
        _aiWorkoutGeneratorService = aiWorkoutGeneratorService;
        _logger = logger;
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

        if (string.IsNullOrWhiteSpace(request.Goal))
        {
            return BadRequest(new { message = "Goal is required." });
        }

        if (request.DurationMinutes.HasValue && request.DurationMinutes.Value < 15)
        {
            return BadRequest(new { message = "Duration must be at least 15 minutes." });
        }

        try
        {
            var userId = User.GetRequiredUserId().ToString(CultureInfo.InvariantCulture);
            var plan = await _aiWorkoutGeneratorService.GenerateAsync(userId, request, cancellationToken);
            return Ok(plan);
        }
        catch (ArgumentException exception)
        {
            _logger.LogWarning(exception, "AI workout generation was rejected due to invalid input.");
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "AI workout generation failed because the current user context could not be resolved.");
            return Problem(
                title: "Unable to generate AI workout plan.",
                detail: "The generator could not resolve the current user context for this request.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected error while generating AI workout plan.");
            return Problem(
                title: "Unable to generate AI workout plan.",
                detail: "An unexpected server error occurred while building the workout plan.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
