var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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

app.Run();

internal sealed record HealthResponse(string Status);

public partial class Program;
