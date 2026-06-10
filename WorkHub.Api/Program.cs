using System.Text;
using Amazon.S3;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
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

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
