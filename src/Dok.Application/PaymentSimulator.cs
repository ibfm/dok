using Dok.Application.Abstractions;

namespace Dok.Application;

public sealed class PaymentSimulator : IPaymentSimulator
{
    private const decimal PixDiscount = 0.05m;
    private const decimal CardMonthlyRate = 0.025m;
    private static readonly int[] InstallmentCounts = [1, 6, 12];

    public IReadOnlyList<PaymentOption> Simulate(IReadOnlyList<UpdatedDebt> debts)
    {
        if (debts.Count == 0)
            return [];

        var options = new List<PaymentOption>();

        // TOTAL
        var totalBase = Money.Of(debts.Sum(d => d.UpdatedAmount.Value));
        options.Add(BuildOption("TOTAL", totalBase));

        // SOMENTE_<TIPO> (singular, agrupa por tipo mesmo com múltiplos débitos do mesmo tipo)
        var byType = debts
            .GroupBy(d => d.Type)
            .OrderBy(g => g.Key);

        foreach (var group in byType)
        {
            var typeBase = Money.Of(group.Sum(d => d.UpdatedAmount.Value));
            var tag = $"SOMENTE_{DebtTypeMapper.ToWire(group.Key)}";
            options.Add(BuildOption(tag, typeBase));
        }

        return options;
    }

    private static PaymentOption BuildOption(string tag, Money baseAmount)
    {
        var pix = new PixOption(Money.Of(baseAmount.Value * (1m - PixDiscount)));
        var card = new CreditCardOption(BuildInstallments(baseAmount));
        return new PaymentOption(tag, baseAmount, pix, card);
    }

    private static IReadOnlyList<Installment> BuildInstallments(Money baseAmount)
    {
        var installments = new List<Installment>(InstallmentCounts.Length);
        foreach (var n in InstallmentCounts)
        {
            if (n == 1)
            {
                installments.Add(new Installment(1, baseAmount));
                continue;
            }

            // Price/PMT: base * i * (1+i)^n / ((1+i)^n − 1)
            var i = (double)CardMonthlyRate;
            var factor = Math.Pow(1d + i, n);
            var pmtRaw = (decimal)((double)baseAmount.Value * i * factor / (factor - 1d));
            installments.Add(new Installment(n, Money.Of(pmtRaw)));
        }
        return installments;
    }
}
