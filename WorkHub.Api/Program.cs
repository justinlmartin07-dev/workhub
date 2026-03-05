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
    options.UseNpgsql(databaseUrl));

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
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
