using BuildingBlocks.Common;
using BuildingBlocks.Messaging;
using BuildingBlocks.Observability;
using BuildingBlocks.Security;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Payment.Application;
using Payment.Infrastructure;
using Polly;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Host.UsePlatformSerilog();
builder.Services.AddPlatformObservability(serviceName: "payment-service");

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PaymentDb")));

builder.Services.AddPlatformJwtAuthentication();

builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IValidator<CapturePaymentCommand>, CapturePaymentCommandValidator>();
builder.Services.AddScoped<CapturePaymentCommandHandler>();
builder.Services.AddScoped<IValidator<CreatePaymentCommand>, CreatePaymentCommandValidator>();
builder.Services.AddScoped<CreatePaymentCommandHandler>();
builder.Services.AddScoped<GetPaymentByIdQueryHandler>();

// Outbox dispatch - PaymentCaptured/PaymentFailed events written by
// PaymentRepository.EnqueueEvent (same transaction as the terminal-status save,
// per CapturePaymentCommandHandler) are picked up by the background dispatcher
// registered here and published to RabbitMQ.
builder.Services.AddOutboxDispatcher();
builder.Services.AddScoped<IOutboxStore, PaymentOutboxStore>();

// Wallet's availability directly gates whether the capture saga can proceed, so
// its client gets retry + circuit-breaker resilience per
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

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("PaymentDb")!, tags: [HealthCheckEndpointExtensions.ReadyTag]);

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

app.UseCorrelationId();
app.UseProblemDetailsExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapPlatformHealthChecks();
app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.Run();
