using EstateAggregator.DTOs;
using EstateAggregator.Services;
using Microsoft.AspNetCore.Mvc;

namespace EstateAggregator.Controllers;

// Manual trigger for the scraper orchestration pipeline. Useful for testing
// end-to-end wiring (dedup, ScraperRun logging, report generation) without
// waiting for the scheduled 8am/5pm Quartz cron, and will remain useful once
// real scraper integrations exist.
[ApiController]
[Route("api/[controller]")]
public class ScrapersController : ControllerBase
{
    private readonly ScraperService _scraperService;

    public ScrapersController(ScraperService scraperService)
    {
        _scraperService = scraperService;
    }

    [HttpPost("run-now")]
    public async Task<ActionResult<ScraperReportDto>> RunNow(CancellationToken cancellationToken)
    {
        var report = await _scraperService.RunScrapersAsync(cancellationToken);
        return Ok(report);
    }
}
