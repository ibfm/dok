using Dok.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Dok.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddDokApplication(this IServiceCollection services)
    {
        services.AddSingleton<IInterestRule, IpvaInterestRule>();
        services.AddSingleton<IInterestRule, MultaInterestRule>();
        services.AddSingleton<IInterestRule, LicenciamentoInterestRule>();
        services.AddSingleton<IReadOnlyDictionary<DebtType, IInterestRule>>(sp =>
            sp.GetServices<IInterestRule>().ToDictionary(r => r.Type));

        services.AddScoped<IDebtsCalculator, DebtsCalculator>();
        services.AddSingleton<IPaymentSimulator, PaymentSimulator>();
        services.AddScoped<IDebtsService, DebtsService>();

        return services;
    }
}
