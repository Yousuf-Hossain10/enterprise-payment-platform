using BuildingBlocks.Common;
using FluentValidation;
using Identity.Domain;

namespace Identity.Application;

public record RegisterUserCommand(string Email, string Password);

public class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(12);
    }
}

public class RegisterUserCommandHandler
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IValidator<RegisterUserCommand> _validator;

    public RegisterUserCommandHandler(
        IUserRepository users, IPasswordHasher passwordHasher, IValidator<RegisterUserCommand> validator)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _validator = validator;
    }

    public async Task<Result<Guid>> HandleAsync(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<Guid>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        if (await _users.GetByEmailAsync(command.Email, cancellationToken) is not null)
            return Result<Guid>.Failure("Email is already registered.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = command.Email,
            PasswordHash = _passwordHasher.Hash(command.Password),
            Roles = [],
            CreatedAtUtc = DateTime.UtcNow
        };

        await _users.AddAsync(user, cancellationToken);
        return Result<Guid>.Success(user.Id);
    }
}
