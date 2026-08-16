using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Common.Tests;

public class OptionsBuilderExtensionsTests
{
    private class TestOptions
    {
        [Required]
        public string? ConnectionString { get; set; }
    }

    private static IOptions<TestOptions> BuildOptions(IDictionary<string, string?> configValues)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddValidatedOptions<TestOptions>("Test");

        return services.BuildServiceProvider().GetRequiredService<IOptions<TestOptions>>();
    }

    [Fact]
    public void BindsConfigurationSection_ToTypedOptions()
    {
        var options = BuildOptions(new Dictionary<string, string?>
        {
            ["Test:ConnectionString"] = "Host=localhost"
        });

        Assert.Equal("Host=localhost", options.Value.ConnectionString);
    }

    [Fact]
    public void ThrowsOptionsValidationException_WhenRequiredValueMissing()
    {
        var options = BuildOptions(new Dictionary<string, string?>());

        Assert.Throws<OptionsValidationException>(() => options.Value);
    }
}
