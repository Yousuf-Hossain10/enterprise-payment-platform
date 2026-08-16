using FluentValidation;

namespace BuildingBlocks.Common;

/// <summary>
/// Every financial write request carries an Idempotency-Key (docs/API-Guidelines.md).
/// Service-specific validators for those requests inherit from this instead of
/// re-declaring the same rule.
/// </summary>
public interface IIdempotentRequest
{
    string IdempotencyKey { get; }
}

public abstract class IdempotentRequestValidatorBase<T> : AbstractValidator<T> where T : IIdempotentRequest
{
    protected IdempotentRequestValidatorBase()
    {
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .WithMessage("Idempotency-Key is required for this operation.");
    }
}
