using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Amazon.S3;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WorkHub.Api.Data;
using WorkHub.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Kestrel 50MB request body limit
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 50 * 1024 * 1024;
});

// Database
var databaseUrl = builder.Configuration["DATABASE_URL"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<WorkHubDbContext>(options =>
    options.UseNpgsql(ToNpgsqlConnectionString(databaseUrl)));

// Railway provides DATABASE_URL in URL form (postgresql://user:pass@host:port/db),
// which Npgsql can't parse. Convert it to a keyword connection string. A value
// already in keyword form (local dev) is passed through unchanged.
static string? ToNpgsqlConnectionString(string? value)
{
    if (string.IsNullOrWhiteSpace(value)) return value;
    if (!value.StartsWith("postgres://") && !value.StartsWith("postgresql://")) return value;

    var uri = new Uri(value);
    var userInfo = uri.UserInfo.Split(':', 2);
    var csb = new Npgsql.NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Username = Uri.UnescapeDataString(userInfo[0]),
        Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
        Database = uri.AbsolutePath.TrimStart('/'),
    };

    // Carry over sslmode if the URL specifies one (Railway's public proxy needs SSL).
    foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
    {
        var kv = pair.Split('=', 2);
        if (kv.Length == 2 && kv[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase)
            && Enum.TryParse<Npgsql.SslMode>(kv[1], true, out var mode))
            csb.SslMode = mode;
    }

    return csb.ConnectionString;
}

// JWT Authentication
var jwtKey = builder.Configuration["JWT_SECRET_KEY"]
    ?? builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("JWT secret key not configured");

// HS256 requires a key of at least 256 bits (32 bytes). Fail fast on a weak key
// rather than issuing brute-forceable tokens.
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("JWT secret key must be at least 32 bytes (256 bits).");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "workhub-api",
            ValidAudience = "workhub-app",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero,
        };
    });

builder.Services.AddAuthorization();

// R2 / S3
var r2AccountId = builder.Configuration["R2_ACCOUNT_ID"] ?? builder.Configuration["R2:AccountId"];
if (!string.IsNullOrEmpty(r2AccountId))
{
    builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
        builder.Configuration["R2_ACCESS_KEY_ID"] ?? builder.Configuration["R2:AccessKeyId"],
        builder.Configuration["R2_SECRET_ACCESS_KEY"] ?? builder.Configuration["R2:SecretAccessKey"],
        new AmazonS3Config
        {
            ServiceURL = $"https://{r2AccountId}.r2.cloudflarestorage.com",
            ForcePathStyle = true,
        }));
}
else
{
    // Local dev fallback
    builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client("test", "test", new AmazonS3Config
    {
        ServiceURL = "http://localhost:9000",
        ForcePathStyle = true,
    }));
}

// Services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PhotoService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<AddressService>();
builder.Services.AddHostedService<TokenCleanupService>();

// Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// CORS — native MAUI clients are not subject to CORS, so default to allowing no
// browser origins. Set Cors:AllowedOrigins in config to opt specific origins in.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader();
    });
});

// Trust Railway's proxy so RemoteIpAddress / scheme reflect the real client.
// ForwardLimit = 1 means only the single right-most X-Forwarded-For entry (the one
// Railway's edge appends) is honored, so a client cannot spoof its source IP by
// supplying its own X-Forwarded-For — which would otherwise defeat the per-IP
// rate limiter or let an attacker poison a victim's partition.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Rate limiting — per-IP throttle on the unauthenticated auth endpoints (blunts
// credential stuffing / spraying), and a per-user throttle on the metered
// third-party (Google Places) endpoints to cap billing/quota abuse.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
            }));
    options.AddPolicy("thirdparty", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
            }));
});

var app = builder.Build();

// Auto-migrate and seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WorkHubDbContext>();
    db.Database.Migrate();
    await SeedData.SeedAsync(db);
    await SeedData.SeedContactLabelsAsync(db);
}

app.UseForwardedHeaders();

// Standardized error responses — never leak stack traces; map auth failures to 401.
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var error = context.Features.Get<IExceptionHandlerPathFeature>()?.Error;
        if (error is WorkHub.Api.Controllers.MissingUserClaimException)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
        }
        else
        {
            context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("UnhandledException")
                .LogError(error, "Unhandled exception for {Method} {Path}",
                    context.Request.Method, context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
        }
    });
});

if (!app.Environment.IsDevelopment())
    app.UseHsts();

// Baseline security headers.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
