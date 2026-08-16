using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace BuildingBlocks.Common.Tests;

public class ProblemDetailsMiddlewareExtensionsTests
{
    private static TestServer CreateServer(string environmentName)
    {
        var builder = new WebHostBuilder()
            .UseEnvironment(environmentName)
            .Configure(app =>
            {
                app.UseProblemDetailsExceptionHandler();
                app.Run(_ => throw new InvalidOperationException("boom"));
            });

        return new TestServer(builder);
    }

    [Fact]
    public async Task UnhandledException_ReturnsProblemJsonWith500()
    {
        using var server = CreateServer(Environments.Production);
        using var client = server.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("An unexpected error occurred", problem!.Title);
    }

    [Fact]
    public async Task UnhandledException_HidesExceptionMessageOutsideDevelopment()
    {
        using var server = CreateServer(Environments.Production);
        using var client = server.CreateClient();

        var response = await client.GetAsync("/");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Null(problem!.Detail);
    }

    [Fact]
    public async Task UnhandledException_IncludesExceptionMessageInDevelopment()
    {
        using var server = CreateServer(Environments.Development);
        using var client = server.CreateClient();

        var response = await client.GetAsync("/");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal("boom", problem!.Detail);
    }
}
