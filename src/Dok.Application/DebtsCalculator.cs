using Dok.Application.Abstractions;

namespace Dok.Application;

public sealed class DebtsCalculator(
    IDebtProviderChain providers,
    IReadOnlyDictionary<DebtType, IInterestRule> rules,
    IDebtsClock clock) : IDebtsCalculator
{
    public async Task<CalculatorResult> CalculateAsync(Plate plate, CancellationToken ct)
    {
        var debts = await providers.FetchDebtsAsync(plate, ct);
        var today = clock.Today;

        var updated = new List<UpdatedDebt>(debts.Count);
        foreach (var debt in debts)
        {
            if (!rules.TryGetValue(debt.Type, out var rule))
                throw new UnknownDebtTypeException(DebtTypeMapper.ToWire(debt.Type));
            updated.Add(rule.Apply(debt, today));
        }

        var totalOriginal = Money.Of(updated.Sum(d => d.OriginalAmount.Value));
        var totalUpdated = Money.Of(updated.Sum(d => d.UpdatedAmount.Value));

        return new CalculatorResult(updated, new DebtsSummary(totalOriginal, totalUpdated));
    }
}
