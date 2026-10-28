using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using CarbonTracker.API.Contracts.Authentication;
using CarbonTracker.API.Services.Interfaces;
using CarbonTracker.API.Services;

namespace CarbonTracker.API.Controllers;

[ApiController]
[Route("api/auth")]
[SwaggerTag("Authentication (register, login, refresh, logout, password reset)")]
public class AuthenticationController : ControllerBase
{
    private readonly IUsersService _users;
    private readonly ITokenService _tokens;

    public AuthenticationController(IUsersService users, ITokenService tokens)
    {
        _users = users;
        _tokens = tokens;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "Register a new user")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        try
        {
            await _users.CreateUserAsync(req.Username, req.Email, req.Password);
        }
        catch (DuplicateAccountException)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Username or email already exists.");
        }
        return Ok(new { message = "Account created." });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "Login with username OR email + password")]
    public async Task<ActionResult<TokenResponse>> Login([FromBody] LoginRequest req)
    {
        var user = await _users.FindByIdentifierAsync(req.Identifier);
        if (user is null) return InvalidCredentials();

        var valid = await _users.VerifyPasswordAsync(user.Value.UserId, req.Password);
        if (!valid) return InvalidCredentials();

        var tokens = await _users.IssueTokensAsync(user.Value.UserId, user.Value.Username);
        Response.Headers.CacheControl = "no-store";
        return Ok(new TokenResponse(tokens.AccessToken, tokens.RefreshToken));
    }

    [HttpPost("request-reset")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "Request a password reset (email-based)")]
    public async Task<IActionResult> RequestPasswordReset([FromBody] RequestPasswordResetRequest req)
    {
        // Intentionally return a generic message regardless of account existence
        await _users.CreatePasswordResetAsync(req.Email);
        return Ok(new { message = "If the email exists, a reset link will be sent." });
    }

    [HttpPost("reset")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "Reset password using a reset token")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        try
        {
            await _users.ResetPasswordAsync(req.Token, req.NewPassword);
        }
        catch (InvalidResetTokenException)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid or expired token.");
        }
        return Ok(new { message = "Password updated." });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "Exchange a refresh token for a new access/refresh pair (single use)")]
    public async Task<ActionResult<TokenResponse>> Refresh([FromBody] RefreshTokenRequest req)
    {
        try
        {
            var tokens = await _users.RefreshAsync(req.RefreshToken);
            Response.Headers.CacheControl = "no-store";
            return Ok(new TokenResponse(tokens.AccessToken, tokens.RefreshToken));
        }
        catch (InvalidRefreshTokenException)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid or expired refresh token.");
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "Revoke the supplied refresh token (idempotent)")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest req)
    {
        await _users.RevokeAsync(req.RefreshToken);
        return NoContent();
    }

    private ObjectResult InvalidCredentials() =>
        Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid username or password.");

    [HttpGet("me")]
    [Authorize]
    [SwaggerOperation(Summary = "Who am I? (requires Bearer token)")]
    public IActionResult Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var username = User.Identity?.Name;
        return Ok(new { userId, username });
    }
}
