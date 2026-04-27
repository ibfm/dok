namespace Dok.Domain.Rules;

public sealed class IpvaInterestRule : IInterestRule
{
    private const decimal DailyRate = 0.0033m;
    private const decimal Cap = 0.20m;

    public DebtType Type => DebtType.Ipva;

    public UpdatedDebt Apply(Debt debt, DateOnly today)
    {
        var days = today.DayNumber - debt.DueDate.DayNumber;
        if (days <= 0)
            return new UpdatedDebt(debt.Type, debt.OriginalAmount, debt.OriginalAmount, debt.DueDate, 0);

        var raw = debt.OriginalAmount.Value * DailyRate * days;
        var capValue = debt.OriginalAmount.Value * Cap;
        var interest = Math.Min(raw, capValue);
        var updated = Money.Of(debt.OriginalAmount.Value + interest);
        return new UpdatedDebt(debt.Type, debt.OriginalAmount, updated, debt.DueDate, days);
    }
}
