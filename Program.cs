using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using XtreamStrmOrganizer;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseWebRoot("wwwroot");
builder.Services.AddSingleton<AccountStore>();
builder.Services.AddSingleton<SettingsStore>();
builder.Services.AddSingleton<Organizer>();
builder.Services.AddHostedService<AutoUpdateService>();
builder.Services.AddHttpClient("xtream", c =>
{
    c.Timeout = TimeSpan.FromSeconds(120);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("XtreamStrmOrganizer/1.7");
});
var configFile = Path.GetFullPath(builder.Configuration["CONFIG_FILE"] ?? "/app/data/config.json");
var keysDir = Path.Combine(Path.GetDirectoryName(configFile)!, "keys");
Directory.CreateDirectory(keysDir);
builder.Services.AddDataProtection()
    .SetApplicationName("XtreamStrmOrganizer")
    .PersistKeysToFileSystem(new DirectoryInfo(keysDir));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "xtream_strm_session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
        options.LoginPath = "/login";
    });
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 8, Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0, AutoReplenishment = true
        }));
});

var app = builder.Build();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.Use(async (ctx, next) =>
{
    var account = ctx.RequestServices.GetRequiredService<AccountStore>();
    var path = ctx.Request.Path;
    var publicPath = path == "/health" || path == "/api/auth/setup-status" || path == "/api/auth/setup"
        || path == "/api/auth/login" || path == "/login" || path == "/login.html"
        || path == "/setup" || path == "/setup.html";
    if (!account.Initialized && !publicPath)
    {
        if (path.StartsWithSegments("/api")) ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        else ctx.Response.Redirect("/setup");
        return;
    }
    if (account.Initialized && !publicPath && ctx.User.Identity?.IsAuthenticated != true)
    {
        if (path.StartsWithSegments("/api")) ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        else ctx.Response.Redirect("/login");
        return;
    }
    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/setup", (AccountStore account) => Results.Redirect(account.Initialized ? "/login" : "/setup.html"));
app.MapGet("/login", (AccountStore account) => Results.Redirect(account.Initialized ? "/login.html" : "/setup"));
app.MapGet("/api/auth/setup-status", (AccountStore account) => Results.Ok(new { initialized = account.Initialized }));
app.MapPost("/api/auth/setup", IResult (AccountStore account, SetupRequest request) =>
{
    try
    {
        if (!account.Setup(request.Username, request.Password))
            return Results.Conflict(new { error = "A conta já foi criada. Faça login." });
        return Results.Ok(new { ok = true });
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
}).RequireRateLimiting("auth");
app.MapPost("/api/auth/login", async Task<IResult> (HttpContext ctx, AccountStore accounts, LoginRequest input) =>
{
    if (!accounts.Initialized) return Results.Conflict(new { error = "Faça o cadastro inicial primeiro." });
    if (!accounts.Verify(input.Username, input.Password))
        return Results.Json(new { error = "Usuário ou senha inválidos." }, statusCode: 401);
    var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [new Claim(ClaimTypes.Name, accounts.Username)], CookieAuthenticationDefaults.AuthenticationScheme));
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
        new AuthenticationProperties { IsPersistent = false });
    return Results.Ok(new { ok = true });
}).RequireRateLimiting("auth");
app.MapPost("/api/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok(new { ok = true });
});
app.MapGet("/api/auth/me", (HttpContext ctx) => Results.Ok(new { username = ctx.User.Identity?.Name }));

app.MapGet("/health", () => Results.Ok(new { ok = true, app = "Xtream STRM Organizer", version = "1.7.3" }));
app.MapGet("/api/profiles", (SettingsStore store) => Results.Ok(store.Profiles()));
app.MapGet("/api/settings", IResult (SettingsStore store, string? id) =>
{
    try { return Results.Ok(store.Public(id)); }
    catch (ArgumentException ex) { return Results.NotFound(new { error = ex.Message }); }
});
app.MapPost("/api/settings", IResult (ConnectionSettings input, SettingsStore store, Organizer engine) =>
{
    if (engine.Snapshot().Running) return Results.Conflict(new { error = "Aguarde a sincronização terminar." });
    try { return Results.Ok(store.Save(input)); }
    catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException)
    { return Results.BadRequest(new { error = e.Message }); }
});
app.MapDelete("/api/profiles/{id}", IResult (string id, SettingsStore store, Organizer engine) =>
{
    if (engine.Snapshot().Running) return Results.Conflict(new { error = "Aguarde a sincronização terminar." });
    return store.Delete(id) ? Results.Ok(new { ok = true }) : Results.NotFound(new { error = "Conexão não encontrada." });
});
app.MapPost("/api/test", async Task<IResult> (Organizer engine, ProfileRequest? input, CancellationToken ct) =>
{
    try
    {
        await engine.VerifySavedAsync(ct, input?.ProfileId);
        return Results.Ok(new { ok = true, message = "Conexão Xtream validada." });
    }
    catch (Exception e) when (e is ArgumentException or InvalidOperationException or UnauthorizedAccessException or HttpRequestException or TaskCanceledException or JsonException)
    { return Results.BadRequest(new { error = "Falha na conexão: verifique URL, usuário, senha e servidor." }); }
});
app.MapGet("/api/status", (Organizer engine) => Results.Ok(engine.Snapshot()));
app.MapPost("/api/start", IResult (Organizer engine, StartRequest? input) =>
{
    try
    {
        if (!engine.Start(input?.ForceRefresh ?? false, input?.ProfileId))
            return Results.Conflict(new { error = "Já existe uma sincronização em andamento." });
        return Results.Accepted("/api/status", new { ok = true });
    }
    catch (Exception e) when (e is ArgumentException or InvalidOperationException or IOException)
    { return Results.BadRequest(new { error = e.Message }); }
});
app.MapPost("/api/cancel", IResult (Organizer engine) => engine.Cancel()
    ? Results.Ok(new { ok = true }) : Results.BadRequest(new { error = "Não existe sincronização ativa." }));
app.Run();

public sealed record LoginRequest(string Username, string Password);
public sealed record SetupRequest(string Username, string Password);
public sealed record StartRequest(bool ForceRefresh = false, string? ProfileId = null);
public sealed record ProfileRequest(string? ProfileId = null);
