using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using W_API.Core.Configuration;
using W_API.Core.Interfaces;

namespace W_API.Infrastructure.BackgroundWorker;

public class IngestionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IngestionSettings _cfg;
    private readonly ILogger<IngestionBackgroundService> _log;

    public IngestionBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<AppSettings> opts,
        ILogger<IngestionBackgroundService> log)
    {
        _scopeFactory = scopeFactory;
        _cfg = opts.Value.Ingestion;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_cfg.EnableBackgroundWorker)
        {
            _log.LogInformation("Worker en arrière-plan désactivé (EnableBackgroundWorker=false).");
            return;
        }

        _log.LogInformation("Worker démarré — intervalle : {Interval}s", _cfg.WatchIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var svc = scope.ServiceProvider.GetRequiredService<IIngestionService>();
                await svc.RunBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Erreur non gérée dans le worker d'ingestion");
            }

            await Task.Delay(TimeSpan.FromSeconds(_cfg.WatchIntervalSeconds), stoppingToken);
        }
    }
}
