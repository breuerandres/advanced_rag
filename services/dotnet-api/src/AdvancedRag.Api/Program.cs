using AdvancedRag.Api.Auth;
using AdvancedRag.Api.Middleware;
using AdvancedRag.Api.Security;
using AdvancedRag.App.Auth;
using AdvancedRag.Infrastructure.Auth;
using AdvancedRag.Infrastructure.Persistence;
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
builder.Services.AddSingleton<IPasswordHashService, Pbkdf2PasswordHashService>();
builder.Services.AddSingleton<ICsrfTokenService, CsrfTokenService>();
builder.Services.AddSingleton<JwtSigningKeyStore>();
builder.Services.AddSingleton<IChatTokenIssuer, ChatTokenIssuer>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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

app.MapAuthEndpoints();

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

internal sealed record HealthResponse(string Status);

public partial class Program;
