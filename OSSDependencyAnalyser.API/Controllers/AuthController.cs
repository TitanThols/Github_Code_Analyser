using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OSSDependencyAnalyzer.API.Data;
using OSSDependencyAnalyzer.API.Infrastructure;
using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.API.Controllers;

[ApiController]
[Authorize]
[Route("api/auth")]
public sealed class AuthController(AppDbContext db, IPasswordHasher<UserAccount> passwordHasher) : ControllerBase
{
    // Verifying against a throwaway hash for unknown emails keeps response times uniform,
    // so the login endpoint can't be used to discover which emails have accounts.
    private static readonly Lazy<string> DummyHash = new(() =>
        new PasswordHasher<UserAccount>().HashPassword(new UserAccount(), Guid.NewGuid().ToString()));

    [AllowAnonymous]
    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        if (await db.UserAccounts.AnyAsync(user => user.Email == email, cancellationToken))
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "An account with this email already exists.");

        var account = new UserAccount
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        account.PasswordHash = passwordHasher.HashPassword(account, request.Password);
        db.UserAccounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);

        await SignInAsync(account);
        return Ok(UserResponse.From(account));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var account = await db.UserAccounts.FirstOrDefaultAsync(user => user.Email == email, cancellationToken);

        var verification = passwordHasher.VerifyHashedPassword(
            account ?? new UserAccount(), account?.PasswordHash ?? DummyHash.Value, request.Password);

        if (account is null || verification == PasswordVerificationResult.Failed)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.");

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            account.PasswordHash = passwordHasher.HashPassword(account, request.Password);
            await db.SaveChangesAsync(cancellationToken);
        }

        await SignInAsync(account);
        return Ok(UserResponse.From(account));
    }

    [HttpGet("me")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var account = await db.UserAccounts.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (account is null)
        {
            // The account was deleted after the cookie was issued.
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Unauthorized();
        }
        return Ok(UserResponse.From(account));
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    private async Task SignInAsync(UserAccount account)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new Claim(ClaimTypes.Name, account.DisplayName),
            new Claim(ClaimTypes.Email, account.Email)
        ], CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}

public sealed record UserResponse(Guid Id, string DisplayName, string Email, DateTimeOffset CreatedAt)
{
    public static UserResponse From(UserAccount account) => new(account.Id, account.DisplayName, account.Email, account.CreatedAt);
}

public sealed class RegisterRequest
{
    [Required, EmailAddress, MaxLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(12, ErrorMessage = "Password must be at least 12 characters."), MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    [Required, MinLength(2), MaxLength(80)]
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required, EmailAddress, MaxLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}
