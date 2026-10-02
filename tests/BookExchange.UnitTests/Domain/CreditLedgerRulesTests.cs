using BookExchange.Domain.Credits;
using BookExchange.Domain.Shared;

namespace BookExchange.UnitTests.Domain;

public sealed class CreditLedgerRulesTests
{
    [Fact]
    public void Starter_event_is_plus_one()
    {
        var userId = Guid.NewGuid();

        var starter = CreditEvent.Starter(userId, DateTimeOffset.UnixEpoch);

        starter.Type.Should().Be(CreditEventType.Starter);
        starter.Amount.Should().Be(1);
        starter.UserId.Should().Be(userId);
        starter.ExchangeRequestId.Should().BeNull();
    }

    [Fact]
    public void Available_balance_can_never_go_negative()
    {
        var account = CreditAccount.Open(Guid.NewGuid());

        var act = () => account.Recalculate(available: -1, held: 0);

        act.Should().Throw<DomainException>().WithMessage("Not enough credits.");
        account.Available.Should().Be(0, "a rejected recalculation changes nothing");
    }

    [Fact]
    public void Held_can_never_go_negative()
    {
        var account = CreditAccount.Open(Guid.NewGuid());

        var act = () => account.Recalculate(available: 1, held: -1);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Recalculate_sets_the_cached_balance()
    {
        var account = CreditAccount.Open(Guid.NewGuid());

        account.Recalculate(available: 2, held: 1);

        account.Available.Should().Be(2);
        account.Held.Should().Be(1);
    }
}
