using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Observability.Tests;

public class SerilogHostBuilderExtensionsTests
{
    [Fact]
    public async Task HostBuildsAndLogsWithoutThrowing()
    {
        using var host = Host.CreateDefaultBuilder()
            .UsePlatformSerilog()
            .Build();

        var logger = host.Services.GetRequiredService<ILogger<SerilogHostBuilderExtensionsTests>>();
        logger.LogInformation("smoke test {Property}", "value");

        await host.StartAsync();
        await host.StopAsync();
    }
}
