using BuildingBlocks.Common;
using BuildingBlocks.Observability;
using BuildingBlocks.Security;
using FluentValidation;
using Identity.Application;
using Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Host.UsePlatformSerilog();
builder.Services.AddPlatformObservability(serviceName: "identity-service");

builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("IdentityDb")));

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPasswordHasher, Argon2idPasswordHasher>();
builder.Services.AddScoped<IValidator<RegisterUserCommand>, RegisterUserCommandValidator>();
builder.Services.AddScoped<IValidator<LoginCommand>, LoginCommandValidator>();
builder.Services.AddScoped<IValidator<RefreshCommand>, RefreshCommandValidator>();
builder.Services.AddScoped<IValidator<RevokeCommand>, RevokeCommandValidator>();
builder.Services.AddScoped<RegisterUserCommandHandler>();
builder.Services.AddScoped<LoginCommandHandler>();
builder.Services.AddScoped<RefreshCommandHandler>();
builder.Services.AddScoped<RevokeCommandHandler>();

// JwtOptions bound here (not the full AddPlatformJwtAuthentication()) because
// Identity issues tokens but has no protected endpoints of its own yet to
// validate incoming bearer tokens against.
builder.Services.AddValidatedOptions<JwtOptions>("Jwt");
builder.Services.AddValidatedOptions<TokenIssuanceOptions>("TokenIssuance");
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IRefreshTokenHasher, Sha256RefreshTokenHasher>();

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("IdentityDb")!, tags: [HealthCheckEndpointExtensions.ReadyTag]);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Identity Service",
        Version = "v1",
        Description = "Authentication and token issuance for the whole platform - " +
            "every other service trusts a token this service issued. " +
            "See docs/Microservice-Responsibilities.md."
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

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
