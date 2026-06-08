using Amazon.Runtime;
using Amazon.S3;
using AdvancedRag.Api.Controllers;
using AdvancedRag.Api.Health;
using AdvancedRag.Api.Middleware;
using AdvancedRag.Api.Security;
using AdvancedRag.App.Audit;
using AdvancedRag.App.Auth;
using AdvancedRag.App.Configuration;
using AdvancedRag.App.DocumentImages;
using AdvancedRag.App.Documents;
using AdvancedRag.App.Reporting;
using AdvancedRag.App.Setup;
using AdvancedRag.App.Users;
using AdvancedRag.App.Viewer;
using AdvancedRag.Infrastructure.Audit;
using AdvancedRag.Infrastructure.Auth;
using AdvancedRag.Infrastructure.Configuration;
using AdvancedRag.Infrastructure.DocumentImages;
using AdvancedRag.Infrastructure.Documents;
using AdvancedRag.Infrastructure.Persistence;
using AdvancedRag.Infrastructure.Reporting;
using AdvancedRag.Infrastructure.Setup;
using AdvancedRag.Infrastructure.Users;
using AdvancedRag.Infrastructure.Viewer;
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
        options.Cookie.Name = "__Host-session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.Path = "/";
        options.SlidingExpiration = false;
        options.Events.OnRedirectToLogin = ApiCookieAuthEvents.WriteUnauthorizedAsync;
        options.Events.OnRedirectToAccessDenied = ApiCookieAuthEvents.WriteForbiddenAsync;
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<FixedWindowRateLimiter>();
builder.Services.AddScoped<IOperationalReadinessChecker, OperationalReadinessChecker>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserAuthRepository, EfUserAuthRepository>();
builder.Services.AddScoped<IEffectiveAccessScopeRepository, EfEffectiveAccessScopeRepository>();
builder.Services.AddScoped<ISessionHandoffRepository, EfSessionHandoffRepository>();
builder.Services.AddScoped<ISessionHandoffService>(services =>
{
    var repository = services.GetRequiredService<ISessionHandoffRepository>();
    var timeProvider = services.GetRequiredService<TimeProvider>();
    return new SessionHandoffService(repository, timeProvider);
});
builder.Services.AddScoped<IUserAccountService, UserAccountService>();
builder.Services.AddScoped<IUserAccountRepository, EfUserAccountRepository>();
builder.Services.AddScoped<ISetupService, SetupService>();
builder.Services.AddScoped<ISetupRepository, EfSetupRepository>();
builder.Services.AddScoped<ITenantConfigService, TenantConfigService>();
builder.Services.AddScoped<ITenantConfigRepository, EfTenantConfigRepository>();
builder.Services.AddScoped<IUserAdministrationService, UserAdministrationService>();
builder.Services.AddScoped<IUserAdministrationRepository, EfUserAdministrationRepository>();
builder.Services.AddScoped<IOrganizationalUnitService, OrganizationalUnitService>();
builder.Services.AddScoped<IOrganizationalUnitRepository, EfOrganizationalUnitRepository>();
builder.Services.AddScoped<IDocumentLifecycleService, DocumentLifecycleService>();
builder.Services.AddScoped<IDocumentRepository, EfDocumentRepository>();
builder.Services.AddScoped<IDocumentAccessPolicy, DocumentAccessPolicy>();
builder.Services.AddScoped<IDocumentAccessPolicyDataSource, EfDocumentAccessPolicyDataSource>();
builder.Services.AddScoped<IDocumentImageService, DocumentImageService>();
builder.Services.AddScoped<IDocumentImageRepository, EfDocumentImageRepository>();
builder.Services.AddScoped<IDocumentImageObjectStorage>(services =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var accessKey = SecretConfiguration.Read(configuration, "S3:AccessKey", "S3:AccessKeyFile");
    var secretKey = SecretConfiguration.Read(configuration, "S3:SecretKey", "S3:SecretKeyFile");
    var bucket = configuration["S3:Bucket"] ?? "advanced-rag-document-images";
    var region = configuration["S3:Region"] ?? "us-east-1";
    var endpoint = configuration["S3:Endpoint"];

    AmazonS3Config s3Config = new()
    {
        ForcePathStyle = true,
    };
    if (!string.IsNullOrWhiteSpace(endpoint))
    {
        s3Config.ServiceURL = endpoint;
        s3Config.AuthenticationRegion = region;
    }
    else
    {
        s3Config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region);
    }

    return new S3DocumentImageObjectStorage(new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), s3Config), bucket);
});
builder.Services.AddScoped<IDocumentImportExtractionService, DocumentImportExtractionService>();
builder.Services.AddScoped<IManagementAuditService, EfManagementAuditService>();
builder.Services.AddScoped<IViewerAccessRepository, EfViewerAccessRepository>();
builder.Services.AddScoped<IViewerSessionHandoffRepository, EfViewerAccessRepository>();
builder.Services.AddScoped<IViewerDocumentCatalogService, ViewerDocumentCatalogService>();
builder.Services.AddScoped<IViewerDocumentGroupSource, EfViewerDocumentGroupSource>();
builder.Services.AddScoped<IViewerAccessService>(services =>
{
    var repository = services.GetRequiredService<IViewerAccessRepository>();
    var handoffs = services.GetRequiredService<IViewerSessionHandoffRepository>();
    var timeProvider = services.GetRequiredService<TimeProvider>();
    var accessScopes = services.GetRequiredService<IEffectiveAccessScopeRepository>();
    var accessPolicy = services.GetRequiredService<IDocumentAccessPolicy>();
    var configuration = services.GetRequiredService<IConfiguration>();
    var docsBaseUrl = configuration["Viewer:DocsBaseUrl"] ?? "https://docs.client.com";
    return new ViewerAccessService(repository, handoffs, docsBaseUrl, timeProvider, accessScopes, accessPolicy);
});
builder.Services.AddScoped<IFeedbackReportingService>(services =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var reportingConnectionString = ResolveReportingDatabaseConnectionString(configuration)
        ?? ResolveAppDatabaseConnectionString(configuration)
        ?? throw new InvalidOperationException("Reporting database connection string is not configured.");
    return new NpgsqlFeedbackReportingService(reportingConnectionString);
});
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
builder.Services.AddSingleton<IDocumentHtmlSanitizer, GanssDocumentHtmlSanitizer>();
builder.Services.AddSingleton<IPasswordHashService, Pbkdf2PasswordHashService>();
builder.Services.AddSingleton<ICsrfTokenService, CsrfTokenService>();
builder.Services.AddSingleton<JwtSigningKeyStore>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (ShouldRunDatabaseMigrations(app.Configuration))
{
    await RunAppDatabaseMigrationsAsync(app);
}

app.UseMiddleware<OperationalRequestLoggingMiddleware>();
app.UseMiddleware<RequestIdMiddleware>();
app.UseAuthentication();
app.UseMiddleware<RateLimitMiddleware>();
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

app.MapGet("/health/ready", async (IOperationalReadinessChecker readiness, CancellationToken ct) =>
    {
        OperationalReadinessResult result = await readiness.CheckAsync(ct);
        return result.IsReady
            ? Results.Ok(new HealthResponse("ok"))
            : Results.Json(
                new HealthResponse("unhealthy", result.FailedChecks),
                statusCode: StatusCodes.Status503ServiceUnavailable);
    })
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

static string? ResolveReportingDatabaseConnectionString(IConfiguration configuration)
{
    var directConnectionString = configuration.GetConnectionString("ReportingDatabase");
    if (!string.IsNullOrWhiteSpace(directConnectionString))
    {
        return directConnectionString;
    }

    var host = configuration["Postgres:ReportingHost"] ?? configuration["Postgres:Host"];
    var database = configuration["Postgres:ReportingDatabase"] ?? configuration["Postgres:Database"];
    var username = configuration["Postgres:ReportingUsername"];
    if (string.IsNullOrWhiteSpace(host)
        || string.IsNullOrWhiteSpace(database)
        || string.IsNullOrWhiteSpace(username))
    {
        return null;
    }

    var password = SecretConfiguration.Read(
        configuration,
        "Postgres:ReportingPassword",
        "Postgres:ReportingPasswordFile");

    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = host,
        Database = database,
        Username = username,
        Password = password,
        SearchPath = "rag,app,public",
    };

    if (int.TryParse(configuration["Postgres:ReportingPort"] ?? configuration["Postgres:Port"], out var port))
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

internal sealed record HealthResponse(string Status, IReadOnlyList<string>? Checks = null);

public partial class Program;
