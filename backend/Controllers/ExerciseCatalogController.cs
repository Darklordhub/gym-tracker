using backend.Contracts;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Authorize]
[Route("api/exercise-catalog")]
public class ExerciseCatalogController : ControllerBase
{
    private readonly ExerciseCatalogService _exerciseCatalogService;

    public ExerciseCatalogController(ExerciseCatalogService exerciseCatalogService)
    {
        _exerciseCatalogService = exerciseCatalogService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ExerciseCatalogItemResponse>>> GetCatalog()
    {
        var items = await _exerciseCatalogService.GetAllAsync();
        return Ok(items);
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<ExerciseCatalogItemResponse>>> Search([FromQuery] string? q)
    {
        var items = await _exerciseCatalogService.SearchAsync(q);
        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ExerciseCatalogItemResponse>> GetById(int id)
    {
        var item = await _exerciseCatalogService.GetByIdAsync(id);

        return item is null ? NotFound() : Ok(item);
    }
}
