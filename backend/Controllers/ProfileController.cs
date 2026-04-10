using backend.Contracts;
using backend.Data;
using backend.Extensions;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ProfileController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly PasswordHasher<AppUser> _passwordHasher;

    public ProfileController(AppDbContext dbContext, PasswordHasher<AppUser> passwordHasher)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
    }

    [HttpGet]
    public async Task<ActionResult<AuthUserResponse>> Get()
    {
        var user = await GetCurrentUserQuery().SingleAsync();
        return Ok(MapUser(user));
    }

    [HttpPut]
    public async Task<ActionResult<AuthUserResponse>> Update(UpdateProfileRequest request)
    {
        var user = await GetCurrentUserQuery().SingleAsync();

        if (request.DateOfBirth.HasValue && request.DateOfBirth.Value > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            ModelState.AddModelError(nameof(request.DateOfBirth), "Date of birth cannot be in the future.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        user.FullName = request.FullName.Trim();
        user.DisplayName = NormalizeOptionalText(request.DisplayName);
        user.DateOfBirth = request.DateOfBirth;
        user.HeightCm = request.HeightCm;
        user.Gender = NormalizeOptionalText(request.Gender);

        await _dbContext.SaveChangesAsync();

        return Ok(MapUser(user));
    }

    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await GetCurrentUserQuery().SingleAsync();
        var currentPassword = request.CurrentPassword;
        var newPassword = request.NewPassword;

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return BadRequest(new { message = "Current password is incorrect." });
        }

        if (currentPassword == newPassword)
        {
            return BadRequest(new { message = "New password must be different from the current password." });
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        await _dbContext.SaveChangesAsync();

        return Ok(new { message = "Password updated." });
    }

    private IQueryable<AppUser> GetCurrentUserQuery()
    {
        var userId = User.GetRequiredUserId();
        return _dbContext.Users.Where(user => user.Id == userId);
    }

    private static AuthUserResponse MapUser(AppUser user)
    {
        return new AuthUserResponse
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            DisplayName = user.DisplayName,
            DateOfBirth = user.DateOfBirth,
            HeightCm = user.HeightCm,
            Gender = user.Gender,
            CreatedAt = user.CreatedAt,
        };
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
