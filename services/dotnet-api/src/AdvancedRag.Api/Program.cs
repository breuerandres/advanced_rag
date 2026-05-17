using AdvancedRag.Api.Controllers;
using AdvancedRag.Api.Middleware;
using AdvancedRag.Api.Security;
using AdvancedRag.App.Auth;
using AdvancedRag.App.Documents;
using AdvancedRag.App.Users;
using AdvancedRag.Infrastructure.Auth;
using AdvancedRag.Infrastructure.Documents;
using AdvancedRag.Infrastructure.Persistence;
using AdvancedRag.Infrastructure.Users;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddDbContext<AppDbContext>((services, options) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var appDatabaseConnectionString = ResolveAppDatabaseConnectionString(configuration)
        ?? throw new InvalidOperationException("App database connection string is not configured.");

    options.UseNpgsql(
        appDatabaseConnectionString,
        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", AppDbContext.Schema));
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "__Host-advanced-rag-session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.Path = "/";
        options.SlidingExpiration = false;
        options.Events.OnRedirectToLogin = ApiCookieAuthEvents.WriteUnauthorizedAsync;
        options.Events.OnRedirectToAccessDenied = ApiCookieAuthEvents.WriteForbiddenAsync;
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserAuthRepository, EfUserAuthRepository>();
builder.Services.AddScoped<IUserAdministrationService, UserAdministrationService>();
builder.Services.AddScoped<IUserAdministrationRepository, EfUserAdministrationRepository>();
builder.Services.AddScoped<IDocumentLifecycleService, DocumentLifecycleService>();
builder.Services.AddScoped<IDocumentRepository, EfDocumentRepository>();
builder.Services.AddScoped<IDocumentImportExtractionService, DocumentImportExtractionService>();
builder.Services.AddHttpClient("InternalIndexing", (services, client) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["RagApi:InternalBaseUrl"]
        ?? configuration["Rag:InternalBaseUrl"]
        ?? "http://rag-api:8000";
    client.BaseAddress = new Uri(baseUrl);
});
builder.Services.AddScoped<IInternalIndexingClient>(services =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var http = services.GetRequiredService<IHttpClientFactory>().CreateClient("InternalIndexing");
    var token = SecretConfiguration.Read(
        configuration,
        "InternalService:Token",
        configuration["InternalService:TokenFile"] is null
            ? "InternalServiceTokenFile"
            : "InternalService:TokenFile");
    return new FastApiInternalIndexingClient(http, token);
});
builder.Services.AddSingleton<IInstructionHtmlSanitizer, GanssInstructionHtmlSanitizer>();
builder.Services.AddSingleton<IPasswordHashService, Pbkdf2PasswordHashService>();
builder.Services.AddSingleton<ICsrfTokenService, CsrfTokenService>();
builder.Services.AddSingleton<JwtSigningKeyStore>();
builder.Services.AddSingleton<IChatTokenIssuer, ChatTokenIssuer>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (ShouldRunDatabaseMigrations(app.Configuration))
{
    await RunAppDatabaseMigrationsAsync(app);
}

app.UseMiddleware<RequestIdMiddleware>();
app.UseAuthentication();
app.UseMiddleware<CsrfProtectionMiddleware>();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health/live", () => Results.Ok(new HealthResponse("ok")))
    .WithName("LiveHealth")
    .WithOpenApi();

app.MapGet("/health/ready", () => Results.Ok(new HealthResponse("ok")))
    .WithName("ReadyHealth")
    .WithOpenApi();

app.MapControllers();

app.Run();

static string? ResolveAppDatabaseConnectionString(IConfiguration configuration)
{
    var directConnectionString = configuration.GetConnectionString("AppDatabase");
    if (!string.IsNullOrWhiteSpace(directConnectionString))
    {
        return directConnectionString;
    }

    var host = configuration["Postgres:Host"];
    var database = configuration["Postgres:Database"];
    var username = configuration["Postgres:Username"];
    if (string.IsNullOrWhiteSpace(host)
        || string.IsNullOrWhiteSpace(database)
        || string.IsNullOrWhiteSpace(username))
    {
        return null;
    }

    var password = SecretConfiguration.Read(
        configuration,
        "Postgres:Password",
        "Postgres:PasswordFile");

    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = host,
        Database = database,
        Username = username,
        Password = password,
    };

    if (int.TryParse(configuration["Postgres:Port"], out var port))
    {
        builder.Port = port;
    }

    return builder.ConnectionString;
}

static bool ShouldRunDatabaseMigrations(IConfiguration configuration)
{
    return configuration.GetValue("Database:RunMigrationsOnStartup", false);
}

static async Task RunAppDatabaseMigrationsAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigrations");
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    logger.LogInformation("Applying app database migrations.");
    await db.Database.MigrateAsync();
    logger.LogInformation("App database migrations applied.");
}

internal sealed record HealthResponse(string Status);

public partial class Program;
