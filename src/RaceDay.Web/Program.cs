using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Services;

// ============================================================
// RaceDay MVC web application — Part 3
// Thin client: every data operation is delegated to the Part 2 API.
// ============================================================

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    // All POST endpoints require an antiforgery token; every form renders one.
    options.Filters.Add<AutoValidateAntiforgeryTokenAttribute>();
});

// Server-side session holds the API session token, user id, role and display name.
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(12);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Name = ".RaceDay.Session";
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<ApiClient>(client =>
{
    var url = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5049";
    client.BaseAddress = new Uri(url);
    client.Timeout = TimeSpan.FromSeconds(60);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "SAMEORIGIN";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self' https://cdn.jsdelivr.net 'unsafe-inline'; style-src 'self' https://cdn.jsdelivr.net 'unsafe-inline'; img-src 'self' data: https:; font-src 'self' https://cdn.jsdelivr.net data:; connect-src 'self'; frame-ancestors 'self'; base-uri 'self'; form-action 'self'";
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "raceday-web" }));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
