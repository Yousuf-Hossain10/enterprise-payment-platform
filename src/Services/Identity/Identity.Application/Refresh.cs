using BuildingBlocks.Common;
using FluentValidation;

namespace Identity.Application;

public record RefreshCommand(string RefreshToken);

public class RefreshCommandValidator : AbstractValidator<RefreshCommand>
{
    public RefreshCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public class RefreshCommandHandler
{
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IRefreshTokenHasher _refreshTokenHasher;
    private readonly IUserRepository _users;
    private readonly ITokenService _tokenService;
    private readonly IValidator<RefreshCommand> _validator;

    public RefreshCommandHandler(
        IRefreshTokenRepository refreshTokens,
        IRefreshTokenHasher refreshTokenHasher,
        IUserRepository users,
        ITokenService tokenService,
        IValidator<RefreshCommand> validator)
    {
        _refreshTokens = refreshTokens;
        _refreshTokenHasher = refreshTokenHasher;
        _users = users;
        _tokenService = tokenService;
        _validator = validator;
    }

    public async Task<Result<TokenPair>> HandleAsync(RefreshCommand command, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<TokenPair>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var tokenHash = _refreshTokenHasher.Hash(command.RefreshToken);
        var existing = await _refreshTokens.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (existing is null || existing.Revoked || existing.ExpiresAtUtc < DateTime.UtcNow)
            return Result<TokenPair>.Failure("Invalid or expired refresh token.");

        var user = await _users.GetByIdAsync(existing.UserId, cancellationToken);
        if (user is null)
            return Result<TokenPair>.Failure("Invalid or expired refresh token.");

        var newTokens = await _tokenService.IssueAsync(user, cancellationToken);
        var newTokenHash = _refreshTokenHasher.Hash(newTokens.RefreshToken);

        // Rotation: revoke the old token and chain it to its replacement for
        // audit, in the same SaveChangesAsync as adding the new one - see
        // IRefreshTokenRepository for why that atomicity matters.
        existing.Revoked = true;
        existing.ReplacedByTokenHash = newTokenHash;

        _refreshTokens.Add(new Domain.RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = newTokenHash,
            ExpiresAtUtc = newTokens.RefreshTokenExpiresAtUtc,
            Revoked = false,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _refreshTokens.SaveChangesAsync(cancellationToken);

        return Result<TokenPair>.Success(newTokens);
    }
}
