using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReviewOrchestrator.Core;
using ReviewOrchestrator.Infrastructure.Application;

namespace ReviewOrchestrator.Infrastructure.Background;

/// <summary>Checks for expired reviewer leases on a fixed interval (ReviewRouting:LeaseCheckIntervalSeconds) and reroutes.</summary>
public sealed class LeaseExpirationService(
    IServiceScopeFactory scopeFactory,
    IOptions<ReviewRoutingOptions> options,
    ILogger<LeaseExpirationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, options.Value.LeaseCheckIntervalSeconds));
        logger.LogInformation("Lease expiration check running every {Interval}", interval);

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<ReviewOrchestrationService>();
                var expired = await orchestrator.ExpireLeasesAsync(stoppingToken);
                if (expired > 0) logger.LogInformation("Lease check expired and rerouted {Count} assignment(s)", expired);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Lease expiration check failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
