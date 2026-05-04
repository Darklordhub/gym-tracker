using backend.Contracts;
using backend.Extensions;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Authorize]
[Route("api/nutrition")]
public class MealsController : ControllerBase
{
    private readonly IMealService _mealService;
    private readonly ILogger<MealsController> _logger;

    public MealsController(IMealService mealService, ILogger<MealsController> logger)
    {
        _mealService = mealService;
        _logger = logger;
    }

    [HttpGet("days/{date}")]
    public async Task<ActionResult<DailyMealsDto>> GetMealsForDate(DateOnly date, CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = User.GetRequiredUserId();
            var result = await _mealService.GetMealsForDateAsync(userId, date, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Meal day lookup failed because the authenticated user context was invalid.");
            return Problem(
                title: "Unable to load meals.",
                detail: "The current user context could not be resolved for this request.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected error while loading meals for date {Date}.", date);
            return Problem(
                title: "Unable to load meals.",
                detail: "An unexpected server error occurred while loading meals.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost("days/{date}/meals")]
    public async Task<ActionResult<UserMealDto>> CreateMeal(
        DateOnly date,
        CreateMealRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var userId = User.GetRequiredUserId();
            var meal = await _mealService.CreateMealAsync(userId, date, request, cancellationToken);
            return CreatedAtAction(nameof(GetMealById), new { mealId = meal.Id }, meal);
        }
        catch (NutritionModeConflictException exception)
        {
            _logger.LogInformation(exception, "Meal creation conflicted with manual calorie mode for date {Date}.", date);
            return Conflict(new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            _logger.LogWarning(exception, "Meal creation request was rejected.");
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Meal creation failed because the authenticated user context was invalid.");
            return Problem(
                title: "Unable to create meal.",
                detail: "The current user context could not be resolved for this request.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected error while creating a meal for date {Date}.", date);
            return Problem(
                title: "Unable to create meal.",
                detail: "An unexpected server error occurred while creating the meal.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost("days/{date}/switch-to-meals")]
    public async Task<ActionResult<DailyMealsDto>> SwitchDayToMeals(DateOnly date, CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = User.GetRequiredUserId();
            var result = await _mealService.SwitchDayToMealsAsync(userId, date, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Switch to meals failed because the authenticated user context was invalid.");
            return Problem(
                title: "Unable to switch to meal tracking.",
                detail: "The current user context could not be resolved for this request.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected error while switching date {Date} to meal tracking.", date);
            return Problem(
                title: "Unable to switch to meal tracking.",
                detail: "An unexpected server error occurred while switching the day to meal tracking.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpGet("meals/{mealId:int}")]
    public async Task<ActionResult<UserMealDto>> GetMealById(int mealId, CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = User.GetRequiredUserId();
            var meal = await _mealService.GetMealByIdAsync(userId, mealId, cancellationToken);
            return meal is null ? NotFound() : Ok(meal);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Meal lookup failed because the authenticated user context was invalid.");
            return Problem(
                title: "Unable to load meal.",
                detail: "The current user context could not be resolved for this request.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected error while loading meal {MealId}.", mealId);
            return Problem(
                title: "Unable to load meal.",
                detail: "An unexpected server error occurred while loading the meal.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPut("meals/{mealId:int}")]
    public async Task<ActionResult<UserMealDto>> UpdateMeal(
        int mealId,
        UpdateMealRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var userId = User.GetRequiredUserId();
            var meal = await _mealService.UpdateMealAsync(userId, mealId, request, cancellationToken);
            return meal is null ? NotFound() : Ok(meal);
        }
        catch (NutritionModeConflictException exception)
        {
            _logger.LogInformation(exception, "Meal update conflicted with manual calorie mode for meal {MealId}.", mealId);
            return Conflict(new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            _logger.LogWarning(exception, "Meal update request was rejected.");
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Meal update failed because the authenticated user context was invalid.");
            return Problem(
                title: "Unable to update meal.",
                detail: "The current user context could not be resolved for this request.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected error while updating meal {MealId}.", mealId);
            return Problem(
                title: "Unable to update meal.",
                detail: "An unexpected server error occurred while updating the meal.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpDelete("meals/{mealId:int}")]
    public async Task<IActionResult> DeleteMeal(int mealId, CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = User.GetRequiredUserId();
            var deleted = await _mealService.DeleteMealAsync(userId, mealId, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Meal deletion failed because the authenticated user context was invalid.");
            return Problem(
                title: "Unable to delete meal.",
                detail: "The current user context could not be resolved for this request.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected error while deleting meal {MealId}.", mealId);
            return Problem(
                title: "Unable to delete meal.",
                detail: "An unexpected server error occurred while deleting the meal.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost("meals/{mealId:int}/items")]
    public async Task<ActionResult<UserMealItemDto>> AddMealItem(
        int mealId,
        AddMealItemRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var userId = User.GetRequiredUserId();
            var item = await _mealService.AddMealItemAsync(userId, mealId, request, cancellationToken);
            return item is null
                ? NotFound()
                : CreatedAtAction(nameof(GetMealById), new { mealId }, item);
        }
        catch (NutritionModeConflictException exception)
        {
            _logger.LogInformation(exception, "Meal item creation conflicted with manual calorie mode for meal {MealId}.", mealId);
            return Conflict(new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            _logger.LogWarning(exception, "Meal item creation request was rejected.");
            return BadRequest(new { message = exception.Message });
        }
        catch (NutritionProviderException exception)
        {
            _logger.LogWarning(exception, "Meal item creation failed while calling the nutrition provider.");
            return Problem(
                title: "Nutrition provider is unavailable.",
                detail: "Nutrition data is currently unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Meal item creation failed because the request could not be completed.");
            return Problem(
                title: "Unable to add meal item.",
                detail: exception.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected error while adding an item to meal {MealId}.", mealId);
            return Problem(
                title: "Unable to add meal item.",
                detail: "An unexpected server error occurred while adding the meal item.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPut("meal-items/{mealItemId:int}")]
    public async Task<ActionResult<UserMealItemDto>> UpdateMealItem(
        int mealItemId,
        UpdateMealItemRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var userId = User.GetRequiredUserId();
            var item = await _mealService.UpdateMealItemAsync(userId, mealItemId, request, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (NutritionModeConflictException exception)
        {
            _logger.LogInformation(exception, "Meal item update conflicted with manual calorie mode for item {MealItemId}.", mealItemId);
            return Conflict(new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            _logger.LogWarning(exception, "Meal item update request was rejected.");
            return BadRequest(new { message = exception.Message });
        }
        catch (NutritionProviderException exception)
        {
            _logger.LogWarning(exception, "Meal item update failed while calling the nutrition provider.");
            return Problem(
                title: "Nutrition provider is unavailable.",
                detail: "Nutrition data is currently unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Meal item update failed because the request could not be completed.");
            return Problem(
                title: "Unable to update meal item.",
                detail: exception.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected error while updating meal item {MealItemId}.", mealItemId);
            return Problem(
                title: "Unable to update meal item.",
                detail: "An unexpected server error occurred while updating the meal item.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpDelete("meal-items/{mealItemId:int}")]
    public async Task<IActionResult> DeleteMealItem(int mealItemId, CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = User.GetRequiredUserId();
            var deleted = await _mealService.DeleteMealItemAsync(userId, mealItemId, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Meal item deletion failed because the request could not be completed.");
            return Problem(
                title: "Unable to delete meal item.",
                detail: exception.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected error while deleting meal item {MealItemId}.", mealItemId);
            return Problem(
                title: "Unable to delete meal item.",
                detail: "An unexpected server error occurred while deleting the meal item.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
