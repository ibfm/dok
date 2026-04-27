using Dok.Api.Time;

namespace Dok.Api.Extensions;

internal static class TimeProviderExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registra o <see cref="TimeProvider"/> da aplicação. Por padrão usa
        /// <see cref="TimeProvider.System"/> (relógio real). Se <c>Domain:ReferenceDate</c>
        /// estiver configurado, usa um <see cref="FixedTimeProvider"/> nessa data — útil
        /// para a demo bater com os exemplos numéricos da spec (2024-05-10).
        /// </summary>
        public IServiceCollection AddDokTimeProvider(IConfiguration config)
        {
            var fixedDate = config.GetValue<DateTimeOffset?>("Domain:ReferenceDate");
            TimeProvider clock = fixedDate is { } d
                ? new FixedTimeProvider(d)
                : TimeProvider.System;
            services.AddSingleton(clock);
            return services;
        }
    }
}
