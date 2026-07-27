using System.Text;
using System.Text.Json;
using DevDocsAI.Api.Configuration;
using DevDocsAI.Api.Infrastructure;
using DevDocsAI.Api.Security;
using DevDocsAI.Application;
using DevDocsAI.Application.Abstractions.Security;
using DevDocsAI.Infrastructure;
using DevDocsAI.Infrastructure.Persistence;
using DevDocsAI.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

// Bootstrap logger so failures during startup are captured.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Structured logging via Serilog, configured from appsettings.
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // Validated options pattern (fail fast at startup).
    builder.Services
        .AddOptions<AppInfoOptions>()
        .Bind(builder.Configuration.GetSection(AppInfoOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    // Application + Infrastructure composition.
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();

    // Authentication (JWT bearer) + authorization.
    var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = jwt.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = "name",
            };
        });
    builder.Services.AddAuthorization();

    // Web API + RFC 7807 problem details + global exception handling.
    builder.Services.AddControllers();
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    // OpenAPI document (served at /openapi/v1.json); Scalar UI added below.
    builder.Services.AddOpenApi();

    // Liveness + database readiness.
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<AppDbContext>("database");

    // CORS for the frontend during local development.
    const string frontendCors = "frontend";
    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(options => options.AddPolicy(frontendCors, policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(); // interactive API docs at /scalar
        ApplyMigrations(app);
    }

    app.UseCors(frontendCors);
    app.UseAuthentication();
    app.UseAuthorization();

    // Health endpoint returns a small JSON payload.
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString() }),
            }));
        },
    });

    app.MapControllers();

    Log.Information("DevDocs AI API starting ({Environment})", app.Environment.EnvironmentName);
    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "DevDocs AI API terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// Best-effort dev convenience: apply pending migrations so `docker compose up`
// yields a ready schema. Failures are logged, not fatal (e.g. DB not yet up).
static void ApplyMigrations(WebApplication app)
{
    try
    {
        using var scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Database migration on startup failed; continuing (is the database reachable?)");
    }
}

// Exposed so integration tests can reference the entry point via WebApplicationFactory.
public partial class Program;
