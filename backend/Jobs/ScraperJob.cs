using EstateAggregator.Services;
using Quartz;

namespace EstateAggregator.Jobs;

public class ScraperJob : IJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScraperJob> _logger;

    public ScraperJob(IServiceScopeFactory scopeFactory, ILogger<ScraperJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = _scopeFactory.CreateScope();
        var scraperService = scope.ServiceProvider.GetRequiredService<ScraperService>();

        try
        {
            await scraperService.RunScrapersAsync(context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled scraper run failed");
        }
    }
}
