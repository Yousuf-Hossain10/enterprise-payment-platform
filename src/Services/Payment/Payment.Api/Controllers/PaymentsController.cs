using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payment.Application;

namespace Payment.Api.Controllers;

/// <summary>
/// Every route requires a valid JWT bearer token (docs/API-Guidelines.md
/// Authentication), validated against the same signing key Identity issues with -
/// no separate permission requirement yet (BuildingBlocks.Security's
/// [RequirePermission] currently doubles Roles as permission strings platform-wide,
/// per JwtTokenService's own note, and no role scheme is defined for payments yet).
/// </summary>
[ApiController]
[Route("api/v1/payments")]
[Produces("application/json")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly CreatePaymentCommandHandler _createHandler;
    private readonly GetPaymentByIdQueryHandler _getByIdHandler;

    public PaymentsController(CreatePaymentCommandHandler createHandler, GetPaymentByIdQueryHandler getByIdHandler)
    {
        _createHandler = createHandler;
        _getByIdHandler = getByIdHandler;
    }

    /// <summary>
    /// Creates a payment. Requires an Idempotency-Key header (docs/API-Guidelines.md
    /// Idempotency) - a missing header is itself a 400, not an implied "generate one
    /// for me". Replaying the same key returns the original payment rather than
    /// creating a second one.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePaymentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            ModelState.AddModelError("Idempotency-Key", "The Idempotency-Key header is required.");
            return ValidationProblem(ModelState);
        }

        var result = await _createHandler.HandleAsync(
            new CreatePaymentCommand(request.AccountId, request.Amount, request.Reference, idempotencyKey),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Payment creation failed",
                detail: result.Error,
                instance: HttpContext.Request.Path);
        }

        var response = PaymentResponse.From(result.Value!);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    /// <summary>Reads a payment by id. No idempotency key required - a plain read has no side effects.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _getByIdHandler.HandleAsync(new GetPaymentByIdQuery(id), cancellationToken);

        if (!result.IsSuccess)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Payment not found",
                Detail = result.Error,
                Instance = HttpContext.Request.Path
            });
        }

        return Ok(PaymentResponse.From(result.Value!));
    }
}

public record CreatePaymentRequest(Guid AccountId, decimal Amount, string Reference);

public record PaymentResponse(Guid Id, Guid AccountId, decimal Amount, string Reference, string Status, DateTime CreatedAtUtc)
{
    public static PaymentResponse From(Payment.Domain.Payment payment) => new(
        payment.Id, payment.AccountId, payment.Amount, payment.Reference,
        payment.Status.ToString(), payment.CreatedAtUtc);
}
