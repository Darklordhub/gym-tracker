using backend.Contracts;
using backend.Extensions;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TrainingIntelligenceController : ControllerBase
{
    private readonly TrainingIntelligenceService _trainingIntelligenceService;

    public TrainingIntelligenceController(TrainingIntelligenceService trainingIntelligenceService)
    {
        _trainingIntelligenceService = trainingIntelligenceService;
    }

    [HttpGet("daily-recommendation")]
    public async Task<ActionResult<TrainingRecommendationResponse>> GetDailyRecommendation()
    {
        var userId = User.GetRequiredUserId();
        var recommendation = await _trainingIntelligenceService.GetDailyRecommendationAsync(userId);

        return Ok(recommendation);
    }
}
