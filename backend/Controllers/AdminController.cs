using backend.Authorization;
using backend.Contracts;
using backend.Data;
using backend.Extensions;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public AdminController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
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
}
