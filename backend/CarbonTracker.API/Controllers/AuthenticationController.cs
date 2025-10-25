using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using CarbonTracker.API.Contracts.Authentication;
using CarbonTracker.API.Services;

namespace CarbonTracker.API.Controllers;

[ApiController]
[Route("api/auth")]
[SwaggerTag("Authentication (register, login, password reset)")]
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
        await _users.CreateUserAsync(req.Username, req.Email, req.Password);
        return Ok(new { message = "Account created." });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "Login with username OR email + password")]
    public async Task<ActionResult<TokenResponse>> Login([FromBody] LoginRequest req)
    {
        var user = await _users.FindByIdentifierAsync(req.Identifier);
        if (user is null) return Unauthorized("Invalid username or password.");

        var valid = await _users.VerifyPasswordAsync(user.Value.UserId, req.Password);
        if (!valid) return Unauthorized("Invalid username or password.");

        var tokens = await _users.IssueTokensAsync(user.Value.UserId, user.Value.Username);
        return Ok(new TokenResponse(tokens.AccessToken, tokens.RefreshToken));
    }

    [HttpPost("request-reset")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "Request a password reset (email-based)")]
    public async Task<IActionResult> RequestPasswordReset([FromBody] RequestPasswordResetRequest req)
    {
        // Intentionally return a generic message regardless of account existence
        var token = await _users.CreatePasswordResetAsync(req.Email);
        return Ok(new { message = "If the email exists, a reset link will be sent.", token });
    }

    [HttpPost("reset")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "Reset password using a reset token")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        await _users.ResetPasswordAsync(req.Token, req.NewPassword);
        return Ok(new { message = "Password updated." });
    }

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
