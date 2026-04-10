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
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly PasswordHasher<AppUser> _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;

    public AuthController(
        AppDbContext dbContext,
        PasswordHasher<AppUser> passwordHasher,
        JwtTokenService jwtTokenService)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var normalizedEmail = NormalizeEmail(request.Email);

        var existingUser = await _dbContext.Users.AnyAsync(user => user.Email == normalizedEmail);
        if (existingUser)
        {
            return Conflict(new { message = "An account with that email already exists." });
        }

        var user = new AppUser
        {
            Email = normalizedEmail,
            CreatedAt = DateTime.UtcNow,
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        return Ok(CreateAuthResponse(user));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await _dbContext.Users.SingleOrDefaultAsync(currentUser => currentUser.Email == normalizedEmail);

        if (user is null)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        return Ok(CreateAuthResponse(user));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AuthUserResponse>> Me()
    {
        var userId = User.GetRequiredUserId();
        var user = await _dbContext.Users.AsNoTracking().SingleAsync(currentUser => currentUser.Id == userId);
        return Ok(MapUser(user));
    }

    private AuthResponse CreateAuthResponse(AppUser user)
    {
        var (token, expiresAtUtc) = _jwtTokenService.CreateToken(user);

        return new AuthResponse
        {
            Token = token,
            ExpiresAtUtc = expiresAtUtc,
            User = MapUser(user),
        };
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

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}
