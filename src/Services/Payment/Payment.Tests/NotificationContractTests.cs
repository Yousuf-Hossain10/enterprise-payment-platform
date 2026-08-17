using System.Text.Json;
using Payment.Domain;

namespace Payment.Tests;

/// <summary>
/// Pins the JSON contract of the events Notification (and Audit) will eventually
/// consume from Payment's outbox - Notification doesn't exist yet
/// (docs/Microservice-Responsibilities.md lists it as a Phase 8 consumer of
/// PaymentCaptured/PaymentFailed), so this test locks the shape a future consumer
/// will depend on rather than verifying against a live subscriber.
///
/// Deliberate divergence from docs/API-Guidelines.md's camelCase convention:
/// that rule is scoped to HTTP request/response bodies ("consistent with the
/// Angular frontend's native serialization expectations") - these are internal
/// outbox event payloads, never touched by the frontend, serialized with
/// System.Text.Json's PascalCase default (no options passed). This matches
/// Wallet's WalletDebited/WalletCredited outbox events (Day 26) exactly - the same
/// choice, made the same way, for the same reason. A .NET consumer (Notification,
/// Audit) deserializes this shape for free using matching record types; this test
/// exists so that choice can't silently drift out of sync between publishers.
/// </summary>
public class NotificationContractTests
{
    [Fact]
    public void PaymentCaptured_serializes_with_exactly_the_documented_field_names_and_types()
    {
        var occurredAt = new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);
        var evt = new PaymentCaptured(
            PaymentId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            AccountId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Amount: 40.50m,
            Reference: "order-42",
            OccurredAtUtc: occurredAt);

        var json = JsonSerializer.Serialize(evt);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(5, root.EnumerateObject().Count());
        Assert.Equal("11111111-1111-1111-1111-111111111111", root.GetProperty("PaymentId").GetString());
        Assert.Equal("22222222-2222-2222-2222-222222222222", root.GetProperty("AccountId").GetString());
        Assert.Equal(40.50m, root.GetProperty("Amount").GetDecimal());
        Assert.Equal("order-42", root.GetProperty("Reference").GetString());
        Assert.Equal(occurredAt, root.GetProperty("OccurredAtUtc").GetDateTime());

        // Round-trips cleanly for a .NET consumer using the same record type -
        // the actual guarantee a future Notification/Audit consumer relies on.
        var roundTripped = JsonSerializer.Deserialize<PaymentCaptured>(json);
        Assert.Equal(evt, roundTripped);
    }

    [Fact]
    public void PaymentFailed_serializes_with_exactly_the_documented_field_names_and_types()
    {
        var occurredAt = new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);
        var evt = new PaymentFailed(
            PaymentId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            AccountId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Amount: 40.50m,
            Reference: "order-42",
            FailureReason: "Insufficient funds.",
            OccurredAtUtc: occurredAt);

        var json = JsonSerializer.Serialize(evt);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(6, root.EnumerateObject().Count());
        Assert.Equal("11111111-1111-1111-1111-111111111111", root.GetProperty("PaymentId").GetString());
        Assert.Equal("22222222-2222-2222-2222-222222222222", root.GetProperty("AccountId").GetString());
        Assert.Equal(40.50m, root.GetProperty("Amount").GetDecimal());
        Assert.Equal("order-42", root.GetProperty("Reference").GetString());
        Assert.Equal("Insufficient funds.", root.GetProperty("FailureReason").GetString());
        Assert.Equal(occurredAt, root.GetProperty("OccurredAtUtc").GetDateTime());

        var roundTripped = JsonSerializer.Deserialize<PaymentFailed>(json);
        Assert.Equal(evt, roundTripped);
    }

    [Fact]
    public void PaymentCaptured_and_PaymentFailed_use_the_type_names_the_outbox_dispatcher_publishes_under()
    {
        // AccountRepository/PaymentRepository.EnqueueEvent (Wallet Day 26, Payment
        // Day 35) both use nameof(TEvent) as the outbox message's Type - and thus
        // the RabbitMQ routing key (RabbitMqMessagePublisher uses Type as the
        // routing key). A future Notification consumer binds its queue to these
        // exact strings, so a rename here is a breaking change to that binding,
        // not just a C# refactor.
        Assert.Equal("PaymentCaptured", nameof(PaymentCaptured));
        Assert.Equal("PaymentFailed", nameof(PaymentFailed));
    }
}
