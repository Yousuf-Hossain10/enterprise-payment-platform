using BuildingBlocks.Common;
using FluentValidation;
using Identity.Domain;

namespace Identity.Application;

public record LoginCommand(string Email, string Password);

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class LoginCommandHandler
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IValidator<LoginCommand> _validator;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IRefreshTokenHasher _refreshTokenHasher;

    public LoginCommandHandler(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IValidator<LoginCommand> validator,
        ITokenService tokenService,
        IRefreshTokenRepository refreshTokens,
        IRefreshTokenHasher refreshTokenHasher)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _validator = validator;
        _tokenService = tokenService;
        _refreshTokens = refreshTokens;
        _refreshTokenHasher = refreshTokenHasher;
    }

    public async Task<Result<TokenPair>> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<TokenPair>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var user = await _users.GetByEmailAsync(command.Email, cancellationToken);

        // Same error for "no such user" and "wrong password" - a distinct message
        // would let a caller enumerate registered emails.
        if (user is null || !_passwordHasher.Verify(command.Password, user.PasswordHash))
            return Result<TokenPair>.Failure("Invalid email or password.");

        var tokens = await _tokenService.IssueAsync(user, cancellationToken);

        await _refreshTokens.AddAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = _refreshTokenHasher.Hash(tokens.RefreshToken),
            ExpiresAtUtc = tokens.RefreshTokenExpiresAtUtc,
            Revoked = false,
            CreatedAtUtc = DateTime.UtcNow
        }, cancellationToken);

        return Result<TokenPair>.Success(tokens);
    }
}
