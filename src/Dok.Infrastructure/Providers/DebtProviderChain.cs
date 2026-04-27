using Dok.Infrastructure.Abstractions;
using Microsoft.Extensions.Logging;

namespace Dok.Infrastructure.Providers;

public sealed class DebtProviderChain(
    IEnumerable<IDebtProvider> providers,
    ProviderUsage usage,
    ILogger<DebtProviderChain> logger) : IDebtProviderChain
{
    public async Task<IReadOnlyList<Debt>> FetchDebtsAsync(Plate plate, CancellationToken ct)
    {
        var failures = new List<Exception>();
        foreach (var provider in providers)
        {
            try
            {
                logger.LogInformation("Querying {Provider} for {@Plate}", provider.Name, plate);
                var result = await provider.FetchAsync(plate, ct);
                logger.LogInformation(
                    "{Provider} returned {Count} debts for {@Plate}",
                    provider.Name, result.Count, plate);
                usage.Mark(provider.Name);
                return result;
            }
            catch (UnknownDebtTypeException)
            {
                // Erro de domínio: não silenciar nem cair pro próximo provider
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "{Provider} failed for {@Plate} — trying next provider",
                    provider.Name, plate);
                failures.Add(ex);
            }
        }
        throw new AllProvidersUnavailableException(failures);
    }
}
