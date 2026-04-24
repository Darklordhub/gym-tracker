using backend.Authorization;
using backend.Contracts;
using backend.Data;
using backend.Extensions;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly PasswordHasher<AppUser> _passwordHasher;
    private readonly IWgerExerciseCatalogSyncService _wgerExerciseCatalogSyncService;
    private readonly ExerciseCatalogService _exerciseCatalogService;

    public AdminController(
        AppDbContext dbContext,
        PasswordHasher<AppUser> passwordHasher,
        IWgerExerciseCatalogSyncService wgerExerciseCatalogSyncService,
        ExerciseCatalogService exerciseCatalogService)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _wgerExerciseCatalogSyncService = wgerExerciseCatalogSyncService;
        _exerciseCatalogService = exerciseCatalogService;
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<AdminUserResponse>>> GetUsers()
    {
        var users = await _dbContext.Users
            .AsNoTracking()
            .OrderBy(user => user.CreatedAt)
            .Select(user => new AdminUserResponse
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                DisplayName = user.DisplayName,
                Role = user.Role,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpPut("users/{id:int}/role")]
    public async Task<ActionResult<AdminUserResponse>> UpdateUserRole(int id, UpdateUserRoleRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var normalizedRole = request.Role.Trim();
        if (!AppRoles.IsValid(normalizedRole))
        {
            return BadRequest(new { message = $"Role must be '{AppRoles.User}' or '{AppRoles.Admin}'." });
        }

        var user = await _dbContext.Users.SingleOrDefaultAsync(currentUser => currentUser.Id == id);
        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        var currentUserId = User.GetRequiredUserId();
        if (currentUserId == user.Id && user.Role == AppRoles.Admin && normalizedRole != AppRoles.Admin)
        {
            var otherActiveAdminExists = await _dbContext.Users.AnyAsync(currentUser =>
                currentUser.Id != user.Id &&
                currentUser.IsActive &&
                currentUser.Role == AppRoles.Admin);

            if (!otherActiveAdminExists)
            {
                return BadRequest(new { message = "You cannot remove the last active admin role from your own account." });
            }
        }

        user.Role = normalizedRole;
        await _dbContext.SaveChangesAsync();

        return Ok(MapUser(user));
    }

    [HttpPut("users/{id:int}/status")]
    public async Task<ActionResult<AdminUserResponse>> UpdateUserStatus(int id, UpdateUserStatusRequest request)
    {
        var user = await _dbContext.Users.SingleOrDefaultAsync(currentUser => currentUser.Id == id);
        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        var currentUserId = User.GetRequiredUserId();
        if (currentUserId == user.Id && !request.IsActive && user.Role == AppRoles.Admin)
        {
            var otherActiveAdminExists = await _dbContext.Users.AnyAsync(currentUser =>
                currentUser.Id != user.Id &&
                currentUser.IsActive &&
                currentUser.Role == AppRoles.Admin);

            if (!otherActiveAdminExists)
            {
                return BadRequest(new { message = "You cannot deactivate your own account while it is the last active admin." });
            }
        }

        user.IsActive = request.IsActive;
        await _dbContext.SaveChangesAsync();

        return Ok(MapUser(user));
    }

    [HttpPost("users/{id:int}/reset-password")]
    public async Task<IActionResult> ResetUserPassword(int id, ResetUserPasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var user = await _dbContext.Users.SingleOrDefaultAsync(currentUser => currentUser.Id == id);
        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        var newPassword = request.NewPassword.Trim();
        if (newPassword.Length < 8)
        {
            return BadRequest(new { message = "Password must be at least 8 characters long." });
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        await _dbContext.SaveChangesAsync();

        return Ok(new { message = "Password has been reset successfully." });
    }

    [HttpPost("exercise-catalog/sync-wger")]
    public async Task<ActionResult<ExerciseCatalogSyncResponse>> SyncExerciseCatalogFromWger(CancellationToken cancellationToken)
    {
        var result = await _wgerExerciseCatalogSyncService.SyncAsync(cancellationToken);

        if (!result.IsEnabled)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpGet("exercise-catalog")]
    public async Task<ActionResult<IReadOnlyList<AdminExerciseCatalogItemResponse>>> GetExerciseCatalog([FromQuery] string? q)
    {
        var items = await _exerciseCatalogService.GetAdminItemsAsync(q);
        return Ok(items);
    }

    [HttpPut("exercise-catalog/{id:int}")]
    public async Task<ActionResult<AdminExerciseCatalogItemResponse>> UpdateExerciseCatalogItem(int id, UpdateExerciseCatalogItemRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var item = await _dbContext.ExerciseCatalogItems.SingleOrDefaultAsync(entry => entry.Id == id);
        if (item is null)
        {
            return NotFound(new { message = "Exercise catalog item not found." });
        }

        var normalizedName = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return BadRequest(new { message = "Name is required." });
        }

        if (!IsValidOptionalUrl(request.ThumbnailUrl))
        {
            return BadRequest(new { message = "Thumbnail URL must be a valid absolute HTTP or HTTPS URL." });
        }

        if (!IsValidOptionalUrl(request.VideoUrl))
        {
            return BadRequest(new { message = "Video URL must be a valid absolute HTTP or HTTPS URL." });
        }

        item.LocalNameOverride = string.Equals(normalizedName, item.Name, StringComparison.Ordinal) ? null : normalizedName;
        item.LocalInstructionsOverride = NormalizeOptionalText(request.Instructions, item.Instructions);
        item.LocalThumbnailUrlOverride = NormalizeOptionalText(request.ThumbnailUrl, item.ThumbnailUrl);
        item.LocalVideoUrlOverride = NormalizeOptionalText(request.VideoUrl, item.VideoUrl);
        item.IsActive = request.IsActive;
        item.IsManuallyEdited = HasOverrides(item);
        item.LastEditedAt = item.IsManuallyEdited ? DateTime.UtcNow : null;
        item.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        var response = await _exerciseCatalogService.GetAdminItemByIdAsync(item.Id);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost("exercise-catalog/{id:int}/reset-provider")]
    public async Task<ActionResult<AdminExerciseCatalogItemResponse>> ResetExerciseCatalogItemToProvider(int id)
    {
        var item = await _dbContext.ExerciseCatalogItems.SingleOrDefaultAsync(entry => entry.Id == id);
        if (item is null)
        {
            return NotFound(new { message = "Exercise catalog item not found." });
        }

        item.LocalNameOverride = null;
        item.LocalInstructionsOverride = null;
        item.LocalThumbnailUrlOverride = null;
        item.LocalVideoUrlOverride = null;
        item.IsManuallyEdited = false;
        item.LastEditedAt = null;
        item.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        var response = await _exerciseCatalogService.GetAdminItemByIdAsync(item.Id);
        return response is null ? NotFound() : Ok(response);
    }

    private static AdminUserResponse MapUser(AppUser user)
    {
        return new AdminUserResponse
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            DisplayName = user.DisplayName,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
        };
    }

    private static bool HasOverrides(ExerciseCatalogItem item)
    {
        return !string.IsNullOrWhiteSpace(item.LocalNameOverride)
            || !string.IsNullOrWhiteSpace(item.LocalInstructionsOverride)
            || !string.IsNullOrWhiteSpace(item.LocalThumbnailUrlOverride)
            || !string.IsNullOrWhiteSpace(item.LocalVideoUrlOverride);
    }

    private static string? NormalizeOptionalText(string? value, string? providerValue)
    {
        var normalizedValue = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        var normalizedProvider = string.IsNullOrWhiteSpace(providerValue) ? null : providerValue.Trim();
        return string.Equals(normalizedValue, normalizedProvider, StringComparison.Ordinal) ? null : normalizedValue;
    }

    private static bool IsValidOptionalUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
