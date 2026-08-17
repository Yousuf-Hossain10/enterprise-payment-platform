using BuildingBlocks.Common;
using BuildingBlocks.Messaging;
using BuildingBlocks.Observability;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wallet.Application;
using Wallet.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Host.UsePlatformSerilog();
builder.Services.AddPlatformObservability(serviceName: "wallet-service");

builder.Services.AddDbContext<WalletDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("WalletDb")));

builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IValidator<DebitCommand>, DebitCommandValidator>();
builder.Services.AddScoped<IValidator<CreditCommand>, CreditCommandValidator>();
builder.Services.AddScoped<DebitCommandHandler>();
builder.Services.AddScoped<CreditCommandHandler>();

// Outbox dispatch - WalletDebited/WalletCredited events written by
// AccountRepository.EnqueueEvent (same transaction as the ledger write) are picked
// up by the background dispatcher registered here and published to RabbitMQ.
// No HTTP endpoints are wired yet (see AuthController-equivalent note in
// Debit.cs/Credit.cs) - the dispatcher runs regardless, since it operates purely
// off what's already durably written to WalletDb.
builder.Services.AddOutboxDispatcher();
builder.Services.AddScoped<IOutboxStore, WalletOutboxStore>();

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("WalletDb")!, tags: [HealthCheckEndpointExtensions.ReadyTag]);

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

app.UseAuthorization();

app.MapControllers();
app.MapPlatformHealthChecks();
app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.Run();
