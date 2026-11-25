using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using System.Threading.RateLimiting;
using System.Globalization;
using System.Text.Json;

var output = AppContext.BaseDirectory;
var builder = WebApplication.CreateBuilder(args);
var frontendOrigin = builder.Configuration["FrontendOrigin"] ?? "http://localhost:5173";
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(frontendOrigin).WithMethods("GET", "POST").WithHeaders("Content-Type").AllowCredentials()));

builder.Services.AddHttpClient<ModelClient>(client => {
    client.BaseAddress = new Uri(builder.Configuration["ModelApiUrl"] ?? "http://127.0.0.1:5091/");
    client.Timeout = TimeSpan.FromSeconds(3);
});
builder.Services.AddSingleton<MeterMonitor>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MeterMonitor>());
var state = Environment.GetEnvironmentVariable("ENERGY_STATE_DIR")
    ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(state);
builder.Services.AddSingleton(new EnergyStore(state, Path.Combine(output,"database")));
builder.Services.AddSingleton<Accounts>();
builder.Services.AddDataProtection().SetApplicationName("BuildingEnergy")
    .PersistKeysToFileSystem(Directory.CreateDirectory(Path.Combine(state, "keys")));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options => {
    options.Cookie.Name = "BuildingEnergy.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.Events.OnRedirectToLogin = ctx => {
        ctx.Response.StatusCode = 401;
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options => {
    options.RejectionStatusCode = 429;
    options.AddPolicy("accounts", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "local",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
});
var app = builder.Build();
app.Use(async (ctx, next) => {
    try { await next(); }
    catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException) && !ctx.RequestAborted.IsCancellationRequested) {
        ctx.Response.StatusCode = 503;
        await ctx.Response.WriteAsJsonAsync(new { error = "Model service unavailable. Start model_service/server.py." });
    }
});
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (ctx, next) => {
    ctx.Response.Headers.CacheControl = "no-store";
    await next();
});
var auth = app.MapGroup("/api/auth");
auth.MapPost("/signup", (Credentials input, Accounts accounts, HttpContext ctx) =>
    accounts.Submit(input, ctx, true)).RequireRateLimiting("accounts");
auth.MapPost("/login", (Credentials input, Accounts accounts, HttpContext ctx) =>
    accounts.Submit(input, ctx, false)).RequireRateLimiting("accounts");
auth.MapPost("/logout", async (HttpContext ctx) => {
    await ctx.SignOutAsync();
    return Results.NoContent();
}).RequireAuthorization();
app.MapGet("/api/health", async (EnergyStore store, ModelClient client, CancellationToken token) => {
    bool trainedModel = await client.Healthy(token);
    bool database = store.Healthy();
    bool healthy = trainedModel && database;
    return Results.Json(new { status = healthy ? "healthy" : "degraded",
        backend = true, trainedModel, database }, statusCode: healthy ? 200 : 503);
});
var energy = app.MapGroup("/api/energy").RequireAuthorization();
energy.MapGet("/monitor", (MeterMonitor monitor) => monitor.Snapshot());
energy.MapGet("/model", (ModelClient client, CancellationToken token) => client.Relay("model", null, token));
energy.MapPost("/predict", (Dictionary<string, double> inputs, ModelClient client, CancellationToken token) =>
    client.Relay("predict", inputs, token));

energy.MapGet("/forecast", async (int? hours, ModelClient client, CancellationToken token) =>
    hours is < 1 or > 168 ? Results.BadRequest("Choose 1 to 168 hours.")
                          : Results.Ok((await client.Forecast(token)).Take(hours ?? 24)));
energy.MapGet("/equipment", (EnergyStore db) => db.Rows(
    "SELECT name, category AS [group], quantity AS count, watts, schedule FROM equipment ORDER BY rowid"));
energy.MapGet("/metrics", (EnergyStore db) => db.Rows(
    "SELECT mae,rmse,mape,r2,baselineMae,trainHours,testHours FROM metrics WHERE id=1").Single());
energy.MapGet("/history", (EnergyStore db) => db.Rows(
    "SELECT h.timestamp,m.energy_kwh AS actual,h.predicted FROM history h JOIN meter m USING(timestamp) ORDER BY h.timestamp"));
energy.MapGet("/dataset", (EnergyStore db) => db.MeterCsv());
app.Run();
