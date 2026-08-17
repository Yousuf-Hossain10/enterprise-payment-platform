using Payment.Domain;

namespace Payment.Tests;

/// <summary>
/// Exhaustively tests every (current status, attempted transition) pair against the
/// business rules the state machine is supposed to enforce - not just the happy
/// path, since "explicit legal-transition guards" (Day 31's actual task) means
/// nothing if illegal transitions aren't proven to actually be rejected.
/// </summary>
public class PaymentStateMachineTests
{
    private static Payment.Domain.Payment DriveTo(PaymentStatus status)
    {
        var payment = new Payment.Domain.Payment { Id = Guid.NewGuid(), AccountId = Guid.NewGuid(), Amount = 10m };

        switch (status)
        {
            case PaymentStatus.Created:
                break;
            case PaymentStatus.Authorized:
                payment.MarkAuthorized();
                break;
            case PaymentStatus.Captured:
                payment.MarkAuthorized();
                payment.MarkCaptured();
                break;
            case PaymentStatus.Settled:
                payment.MarkAuthorized();
                payment.MarkCaptured();
                payment.MarkSettled();
                break;
            case PaymentStatus.Failed:
                payment.MarkAuthorized();
                payment.MarkFailed("seed failure");
                break;
            case PaymentStatus.Refunded:
                payment.MarkAuthorized();
                payment.MarkCaptured();
                payment.MarkRefunded();
                break;
        }

        return payment;
    }

    private static void InvokeTransition(Payment.Domain.Payment payment, PaymentStatus target)
    {
        switch (target)
        {
            case PaymentStatus.Authorized: payment.MarkAuthorized(); break;
            case PaymentStatus.Captured: payment.MarkCaptured(); break;
            case PaymentStatus.Settled: payment.MarkSettled(); break;
            case PaymentStatus.Refunded: payment.MarkRefunded(); break;
            case PaymentStatus.Failed: payment.MarkFailed("reason"); break;
            case PaymentStatus.Created: throw new InvalidOperationException("Created has no Mark method - nothing transitions back to it.");
        }
    }

    // The business rules, stated independently of Payment.cs's own LegalTransitions
    // table, so this test can't just be checking the implementation against itself.
    public static readonly PaymentStatus[] AllTargets =
        [PaymentStatus.Authorized, PaymentStatus.Captured, PaymentStatus.Settled, PaymentStatus.Refunded, PaymentStatus.Failed];

    public static IEnumerable<object[]> AllStatusTransitionPairs()
    {
        var legal = new Dictionary<PaymentStatus, PaymentStatus[]>
        {
            [PaymentStatus.Created] = [PaymentStatus.Authorized, PaymentStatus.Failed],
            [PaymentStatus.Authorized] = [PaymentStatus.Captured, PaymentStatus.Failed],
            [PaymentStatus.Captured] = [PaymentStatus.Settled, PaymentStatus.Refunded],
            [PaymentStatus.Settled] = [PaymentStatus.Refunded],
            [PaymentStatus.Failed] = [],
            [PaymentStatus.Refunded] = []
        };

        foreach (var start in legal.Keys)
        foreach (var target in AllTargets)
            yield return [start, target, legal[start].Contains(target)];
    }

    [Theory]
    [MemberData(nameof(AllStatusTransitionPairs))]
    public void Transition_matches_the_business_rule_for_every_status_pair(
        PaymentStatus start, PaymentStatus target, bool shouldSucceed)
    {
        var payment = DriveTo(start);

        if (shouldSucceed)
        {
            InvokeTransition(payment, target);
            Assert.Equal(target, payment.Status);
        }
        else
        {
            var ex = Assert.Throws<InvalidPaymentStateTransitionException>(() => InvokeTransition(payment, target));
            Assert.Equal(start, ex.CurrentStatus);
            Assert.Equal(target, ex.AttemptedStatus);
            // The rejected attempt must not have mutated state.
            Assert.Equal(start, payment.Status);
        }
    }

    [Fact]
    public void New_payment_starts_in_Created_status()
    {
        var payment = new Payment.Domain.Payment { Id = Guid.NewGuid(), AccountId = Guid.NewGuid(), Amount = 10m };

        Assert.Equal(PaymentStatus.Created, payment.Status);
        Assert.Null(payment.FailureReason);
    }

    [Fact]
    public void MarkFailed_records_the_failure_reason()
    {
        var payment = DriveTo(PaymentStatus.Authorized);

        payment.MarkFailed("wallet debit declined");

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal("wallet debit declined", payment.FailureReason);
    }

    [Fact]
    public void Full_happy_path_reaches_Settled_and_can_still_be_refunded_afterward()
    {
        var payment = new Payment.Domain.Payment { Id = Guid.NewGuid(), AccountId = Guid.NewGuid(), Amount = 10m };

        payment.MarkAuthorized();
        Assert.Equal(PaymentStatus.Authorized, payment.Status);

        payment.MarkCaptured();
        Assert.Equal(PaymentStatus.Captured, payment.Status);

        payment.MarkSettled();
        Assert.Equal(PaymentStatus.Settled, payment.Status);

        payment.MarkRefunded();
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
    }
}
