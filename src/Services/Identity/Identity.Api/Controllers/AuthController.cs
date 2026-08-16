using Identity.Application;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
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

    public record RegisterResponse(Guid Id);

    /// <summary>Create a new user account.</summary>
    /// <remarks>Password must be at least 12 characters.</remarks>
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
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

        var response = new RegisterResponse(result.Value);
        return CreatedAtAction(nameof(Register), new { id = result.Value }, response);
    }

    public record LoginRequest(string Email, string Password);

    /// <summary>Exchange credentials for an access/refresh token pair.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
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

        return Ok(TokenResponse.From(result.Value!));
    }

    public record RefreshRequest(string RefreshToken);

    /// <summary>
    /// Rotate a refresh token: the presented token is revoked and a new
    /// access/refresh pair is issued. A previously-rotated or revoked token
    /// is rejected.
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
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

        return Ok(TokenResponse.From(result.Value!));
    }

    public record LogoutRequest(string RefreshToken);

    /// <summary>Revoke a refresh token immediately, independent of rotation.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
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
}

public record TokenResponse(
    string AccessToken, DateTime AccessTokenExpiresAtUtc, string RefreshToken, DateTime RefreshTokenExpiresAtUtc)
{
    public static TokenResponse From(TokenPair tokens) => new(
        tokens.AccessToken, tokens.AccessTokenExpiresAtUtc, tokens.RefreshToken, tokens.RefreshTokenExpiresAtUtc);
}
