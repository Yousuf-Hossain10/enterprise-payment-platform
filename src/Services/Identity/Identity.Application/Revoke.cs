using BuildingBlocks.Common;
using FluentValidation;

namespace Identity.Application;

public record RevokeCommand(string RefreshToken);

public class RevokeCommandValidator : AbstractValidator<RevokeCommand>
{
    public RevokeCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

/// <summary>
/// Explicit logout: revoke a refresh token immediately, independent of rotation.
/// Lets a caller invalidate a session on demand (e.g. suspected token theft),
/// per docs/Security-Model.md §2's token-theft mitigation commitment.
/// </summary>
public class RevokeCommandHandler
{
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IRefreshTokenHasher _refreshTokenHasher;
    private readonly IValidator<RevokeCommand> _validator;

    public RevokeCommandHandler(
        IRefreshTokenRepository refreshTokens, IRefreshTokenHasher refreshTokenHasher, IValidator<RevokeCommand> validator)
    {
        _refreshTokens = refreshTokens;
        _refreshTokenHasher = refreshTokenHasher;
        _validator = validator;
    }

    public async Task<Result<bool>> HandleAsync(RevokeCommand command, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<bool>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var tokenHash = _refreshTokenHasher.Hash(command.RefreshToken);
        var existing = await _refreshTokens.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (existing is null)
            return Result<bool>.Failure("Invalid refresh token.");

        existing.Revoked = true;
        await _refreshTokens.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
