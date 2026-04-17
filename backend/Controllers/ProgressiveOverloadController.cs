using backend.Contracts;
using backend.Extensions;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ProgressiveOverloadController : ControllerBase
{
    private readonly ProgressiveOverloadService _progressiveOverloadService;

    public ProgressiveOverloadController(ProgressiveOverloadService progressiveOverloadService)
    {
        _progressiveOverloadService = progressiveOverloadService;
    }

    [HttpGet("recommendation")]
    public async Task<ActionResult<ProgressiveOverloadRecommendationResponse>> GetRecommendation(
        [FromQuery] string? exerciseName)
    {
        var userId = User.GetRequiredUserId();
        var recommendation = await _progressiveOverloadService.GetRecommendationAsync(userId, exerciseName);

        return Ok(recommendation);
    }
}
