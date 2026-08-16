using Identity.Application;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly RegisterUserCommandHandler _registerHandler;
    private readonly LoginCommandHandler _loginHandler;

    public AuthController(RegisterUserCommandHandler registerHandler, LoginCommandHandler loginHandler)
    {
        _registerHandler = registerHandler;
        _loginHandler = loginHandler;
    }

    public record RegisterRequest(string Email, string Password);

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _registerHandler.HandleAsync(
            new RegisterUserCommand(request.Email, request.Password), cancellationToken);

        if (!result.IsSuccess)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Registration failed",
                Detail = result.Error,
                Instance = HttpContext.TraceIdentifier
            });
        }

        return CreatedAtAction(nameof(Register), new { id = result.Value }, new { id = result.Value });
    }

    public record LoginRequest(string Email, string Password);

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _loginHandler.HandleAsync(
            new LoginCommand(request.Email, request.Password), cancellationToken);

        if (!result.IsSuccess)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Login failed",
                Detail = result.Error,
                Instance = HttpContext.TraceIdentifier
            });
        }

        var tokens = result.Value!;
        return Ok(new
        {
            accessToken = tokens.AccessToken,
            accessTokenExpiresAtUtc = tokens.AccessTokenExpiresAtUtc,
            refreshToken = tokens.RefreshToken,
            refreshTokenExpiresAtUtc = tokens.RefreshTokenExpiresAtUtc
        });
    }
}
