using BookExchange.Application.Credits;
using BookExchange.Application.Users;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace BookExchange.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);
        services.AddScoped<CreditLedger>();
        services.AddScoped<ProfileService>();
        return services;
    }
}
