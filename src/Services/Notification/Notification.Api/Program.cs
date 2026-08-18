using BuildingBlocks.Common;
using BuildingBlocks.Messaging;
using BuildingBlocks.Observability;
using Notification.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Host.UsePlatformSerilog();
builder.Services.AddPlatformObservability(serviceName: "notification-service");

// Subscribes to the events this service cares about (Microservice-Responsibilities.md:
// PaymentCaptured, PaymentFailed, WalletDebited, WalletCredited). No idempotency
// check or real delivery logic yet - LoggingEventHandler is Day 39's scaffold,
// Day 40 replaces it with the real ProcessedEvents-backed handler.
builder.Services.AddRabbitMqConsumer();
builder.Services.AddScoped<IEventHandler, LoggingEventHandler>();

builder.Services.AddHealthChecks();

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
