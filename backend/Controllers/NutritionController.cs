using backend.Contracts;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Authorize]
[Route("api/nutrition")]
public class NutritionController : ControllerBase
{
    private readonly INutritionService _nutritionService;
    private readonly ILogger<NutritionController> _logger;

    public NutritionController(
        INutritionService nutritionService,
        ILogger<NutritionController> logger)
    {
        _nutritionService = nutritionService;
        _logger = logger;
    }

    [HttpGet("foods/search")]
    public async Task<ActionResult<IReadOnlyList<FoodSearchResultDto>>> SearchFoods(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { message = "Search query 'q' is required." });
        }

        try
        {
            var foods = await _nutritionService.SearchFoodsAsync(q, page, pageSize, cancellationToken);
            return Ok(foods);
        }
        catch (ArgumentException exception)
        {
            _logger.LogWarning(exception, "Nutrition food search request was rejected.");
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Nutrition food search failed because USDA configuration is missing.");
            return Problem(
                title: "Nutrition provider is unavailable.",
                detail: exception.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (NutritionProviderException exception)
        {
            _logger.LogWarning(exception, "Nutrition food search failed while calling USDA.");
            return Problem(
                title: "Nutrition provider is unavailable.",
                detail: "USDA nutrition data is currently unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected error while searching USDA foods.");
            return Problem(
                title: "Unable to search foods.",
                detail: "An unexpected server error occurred while searching nutrition data.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpGet("foods/{source}/{externalId}")]
    public async Task<ActionResult<FoodDetailDto>> GetFoodDetail(
        string source,
        string externalId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var food = await _nutritionService.GetFoodDetailAsync(source, externalId, cancellationToken);

            if (food is null)
            {
                return NotFound();
            }

            return Ok(food);
        }
        catch (ArgumentException exception)
        {
            _logger.LogWarning(exception, "Nutrition food detail request was rejected.");
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Nutrition food detail request failed because USDA configuration is missing.");
            return Problem(
                title: "Nutrition provider is unavailable.",
                detail: exception.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (NutritionProviderException exception)
        {
            _logger.LogWarning(exception, "Nutrition food detail request failed while calling USDA.");
            return Problem(
                title: "Nutrition provider is unavailable.",
                detail: "USDA nutrition data is currently unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected error while fetching USDA food details.");
            return Problem(
                title: "Unable to load food details.",
                detail: "An unexpected server error occurred while loading nutrition data.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost("calculate")]
    public async Task<ActionResult<NutritionCalculateResult>> Calculate(
        NutritionCalculateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var result = await _nutritionService.CalculateAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException exception)
        {
            _logger.LogWarning(exception, "Nutrition calculation request was rejected.");
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Nutrition calculation failed because USDA configuration is missing.");
            return Problem(
                title: "Nutrition provider is unavailable.",
                detail: exception.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (NutritionProviderException exception)
        {
            _logger.LogWarning(exception, "Nutrition calculation failed while calling USDA.");
            return Problem(
                title: "Nutrition provider is unavailable.",
                detail: "USDA nutrition data is currently unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected error while calculating nutrition.");
            return Problem(
                title: "Unable to calculate nutrition.",
                detail: "An unexpected server error occurred while calculating nutrition.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
