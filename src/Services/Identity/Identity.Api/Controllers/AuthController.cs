using Identity.Application;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly RegisterUserCommandHandler _registerHandler;
    private readonly LoginCommandHandler _loginHandler;
    private readonly RefreshCommandHandler _refreshHandler;
    private readonly RevokeCommandHandler _revokeHandler;

    public AuthController(
        RegisterUserCommandHandler registerHandler,
        LoginCommandHandler loginHandler,
        RefreshCommandHandler refreshHandler,
        RevokeCommandHandler revokeHandler)
    {
        _registerHandler = registerHandler;
        _loginHandler = loginHandler;
        _refreshHandler = refreshHandler;
        _revokeHandler = revokeHandler;
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

        return Ok(ToResponse(result.Value!));
    }

    public record RefreshRequest(string RefreshToken);

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var result = await _refreshHandler.HandleAsync(
            new RefreshCommand(request.RefreshToken), cancellationToken);

        if (!result.IsSuccess)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Refresh failed",
                Detail = result.Error,
                Instance = HttpContext.TraceIdentifier
            });
        }

        return Ok(ToResponse(result.Value!));
    }

    public record LogoutRequest(string RefreshToken);

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken cancellationToken)
    {
        var result = await _revokeHandler.HandleAsync(
            new RevokeCommand(request.RefreshToken), cancellationToken);

        if (!result.IsSuccess)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Logout failed",
                Detail = result.Error,
                Instance = HttpContext.TraceIdentifier
            });
        }

        return NoContent();
    }

    private static object ToResponse(TokenPair tokens) => new
    {
        accessToken = tokens.AccessToken,
        accessTokenExpiresAtUtc = tokens.AccessTokenExpiresAtUtc,
        refreshToken = tokens.RefreshToken,
        refreshTokenExpiresAtUtc = tokens.RefreshTokenExpiresAtUtc
    };
}
