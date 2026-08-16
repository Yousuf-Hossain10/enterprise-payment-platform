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

    public LoginCommandHandler(
        IUserRepository users, IPasswordHasher passwordHasher, IValidator<LoginCommand> validator)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _validator = validator;
    }

    public async Task<Result<User>> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<User>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var user = await _users.GetByEmailAsync(command.Email, cancellationToken);

        // Same error for "no such user" and "wrong password" - a distinct message
        // would let a caller enumerate registered emails.
        if (user is null || !_passwordHasher.Verify(command.Password, user.PasswordHash))
            return Result<User>.Failure("Invalid email or password.");

        return Result<User>.Success(user);
    }
}
