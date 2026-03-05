using Microsoft.EntityFrameworkCore;
using WorkHub.Api.Data;

namespace WorkHub.Api.Services;

public class TokenCleanupService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<TokenCleanupService> _logger;

    public TokenCleanupService(IServiceProvider services, ILogger<TokenCleanupService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<WorkHubDbContext>();

                var deleted = await db.RefreshTokens
                    .Where(rt => rt.ExpiresAt < DateTime.UtcNow)
                    .ExecuteDeleteAsync(stoppingToken);

                if (deleted > 0)
                    _logger.LogInformation("Cleaned up {Count} expired refresh tokens", deleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired refresh tokens");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
