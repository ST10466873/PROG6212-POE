using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Config
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "RaceDay API", Version = "v1", Description = "PROG6212 POE Part 2 - RaceDay RESTful API" });
});
builder.Services.AddScoped<AuthService>();

// DbContext - use InMemory when running tests (Environment = Testing), else SQL Server or InMemory fallback
if (builder.Environment.EnvironmentName == "Testing")
{
    builder.Services.AddDbContext<RaceDayContext>(o => o.UseInMemoryDatabase("RaceDay_Test"));
}
else
{
    var conn = builder.Configuration.GetConnectionString("RaceDay");
    if (!string.IsNullOrWhiteSpace(conn))
        builder.Services.AddDbContext<RaceDayContext>(o => o.UseSqlServer(conn));
    else
        builder.Services.AddDbContext<RaceDayContext>(o => o.UseInMemoryDatabase("RaceDay"));
}

var app = builder.Build();

// Ensure DB created + seed roles
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RaceDayContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapGet("/health", () => new { status = "ok", timestamp = DateTime.UtcNow });

app.Run();

// for WebApplicationFactory
public partial class Program { }
