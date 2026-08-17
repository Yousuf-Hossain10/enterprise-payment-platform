using BuildingBlocks.Common;
using Microsoft.Extensions.Options;
using Payment.Application;
using Payment.Infrastructure;
using Polly;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Wallet's availability directly gates whether the capture saga (Day 33) can
// proceed, so its client gets retry + circuit-breaker resilience per
// docs/Enterprise_Payment_Platform_Tutorial.md Phase 7's exact recommended
// pattern: 3 retries with linear backoff, then a 5-failure circuit breaker.
builder.Services.AddValidatedOptions<WalletClientOptions>("WalletClient");
builder.Services.AddHttpClient<IWalletClient, WalletClient>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<WalletClientOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
    })
    .AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(3, i => TimeSpan.FromMilliseconds(200 * i)))
    .AddTransientHttpErrorPolicy(p => p.CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
