namespace Dok.Api.Time;

/// <summary>
/// <see cref="TimeProvider"/> que sempre retorna um instante fixo. Usado para fixar a data
/// de referência da spec (2024-05-10T00:00:00Z) na demo, garantindo que os exemplos numéricos
/// (1800.00, 555.93, 2355.93) batam exatamente com os do enunciado.
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
