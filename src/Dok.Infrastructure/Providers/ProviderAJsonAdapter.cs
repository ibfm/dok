using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Dok.Infrastructure.Abstractions;
using Microsoft.Extensions.Logging;

namespace Dok.Infrastructure.Providers;

public sealed class ProviderAJsonAdapter(
    HttpClient http,
    ILogger<ProviderAJsonAdapter> logger) : IDebtProvider
{
    public string Name => "ProviderA";

    public async Task<IReadOnlyList<Debt>> FetchAsync(Plate plate, CancellationToken ct)
    {
        logger.LogDebug("Calling ProviderA for {@Plate}", plate);
        using var response = await http.GetAsync($"/debts/{plate.Value}", ct);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ProviderAResponse>(ct);
        if (payload?.Debts is null || payload.Debts.Count == 0)
            return Array.Empty<Debt>();

        var result = new List<Debt>(payload.Debts.Count);
        foreach (var item in payload.Debts)
        {
            var type = DebtTypeMapper.Parse(item.Type); // pode lançar UnknownDebtTypeException
            var amount = Money.Of(item.Amount);
            var due = DateOnly.Parse(item.DueDate);
            result.Add(new Debt(type, amount, due));
        }
        return result;
    }

    private sealed record ProviderAResponse(
        [property: JsonPropertyName("vehicle")] string? Vehicle,
        [property: JsonPropertyName("debts")] List<ProviderAItem>? Debts);

    private sealed record ProviderAItem(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("amount")] decimal Amount,
        [property: JsonPropertyName("due_date")] string DueDate);
}
