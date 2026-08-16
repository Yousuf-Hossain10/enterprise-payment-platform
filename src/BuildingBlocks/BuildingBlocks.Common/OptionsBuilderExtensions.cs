using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Common;

public static class OptionsBuilderExtensions
{
    /// <summary>
    /// Binds a config section to a typed options class and validates it via data
    /// annotations on startup (ValidateOnStart), rather than on first use, per
    /// docs/Coding-Standards.md - a missing/invalid config value should crash the
    /// service at boot, not fail the first request that happens to need it.
    /// </summary>
    public static OptionsBuilder<TOptions> AddValidatedOptions<TOptions>(
        this IServiceCollection services,
        string sectionName)
        where TOptions : class
    {
        return services
            .AddOptions<TOptions>()
            .BindConfiguration(sectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}
