using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

// ============================================================
// RaceDay API — entry point
// Builds the host, wires DI, configures EF Core and Swagger,
// ensures the DB is created and seeds the two roles.
// ============================================================

var builder = WebApplication.CreateBuilder(args);

// --- MVC + OpenAPI/Swagger ---
builder.Services.AddControllers(); // attribute-routed controllers (Auth, Users, Events...)
builder.Services.AddEndpointsApiExplorer(); // for Swagger discovery
builder.Services.AddSwaggerGen(c =>
{
    // Swagger doc shown at /swagger — matches docs/api-endpoint-plan.md
    c.SwaggerDoc("v1", new() { Title = "RaceDay API", Version = "v1", Description = "PROG6212 POE Part 2 - RaceDay RESTful API (22 endpoints)" });
});
// AuthService is scoped per request — holds the DbContext
builder.Services.AddScoped<AuthService>();

// --- Database: InMemory for Testing env, else SQL Server or InMemory fallback ---
// Testing env is set by WebApplicationFactory in tests (isolated InMemory DBs)
if (builder.Environment.EnvironmentName == "Testing")
{
    // Each test host gets its own in-memory store via DI override
    builder.Services.AddDbContext<RaceDayContext>(o => o.UseInMemoryDatabase("RaceDay_Test"));
}
else
{
    var conn = builder.Configuration.GetConnectionString("RaceDay");
    if (!string.IsNullOrWhiteSpace(conn))
        builder.Services.AddDbContext<RaceDayContext>(o => o.UseSqlServer(conn)); // LocalDB / SQL Server
    else
        builder.Services.AddDbContext<RaceDayContext>(o => o.UseInMemoryDatabase("RaceDay")); // dev fallback without SQL Server
}

var app = builder.Build();

// --- Ensure DB is created and roles are seeded ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RaceDayContext>();
    db.Database.EnsureCreated(); // creates schema + runs HasData seed (Roles)
}

// --- Middleware pipeline ---
if (app.Environment.IsDevelopment())
{
    // Swagger UI only in Development (not in Testing)
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection(); // enforce HTTPS in production
app.MapControllers(); // map attribute routes
app.MapGet("/", () => Results.Redirect("/swagger")); // root -> swagger convenience
app.MapGet("/health", () => new { status = "ok", timestamp = DateTime.UtcNow }); // liveness probe for Docker/K8s

app.Run();

// For WebApplicationFactory<Program> in integration tests — must be public partial
public partial class Program { }
