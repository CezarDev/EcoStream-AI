using EcoStream.Application.Pipeline;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace EcoStream.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        return services;
    }

    /// <summary>Rule-based incident pipeline (Data Analyst → Risk Decider → Communicator).</summary>
    public static IServiceCollection AddIncidentPipeline(this IServiceCollection services)
    {
        services.AddOptions<PipelineOptions>().BindConfiguration(PipelineOptions.Section);
        // Concrete types are registered too so AI decorators (EcoStream.AI) can use them as fallbacks.
        services.AddScoped<PostGisAffectedAreaAnalyzer>();
        services.AddSingleton<RuleBasedRiskDecider>();
        services.AddSingleton<RuleBasedAlertComposer>();
        services.AddScoped<IAffectedAreaAnalyzer>(sp => sp.GetRequiredService<PostGisAffectedAreaAnalyzer>());
        services.AddSingleton<IRiskDecider>(sp => sp.GetRequiredService<RuleBasedRiskDecider>());
        services.AddSingleton<IAlertComposer>(sp => sp.GetRequiredService<RuleBasedAlertComposer>());
        return services;
    }
}
