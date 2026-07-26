using EstateAggregator.DTOs;
using EstateAggregator.Services;
using Microsoft.AspNetCore.Mvc;

namespace EstateAggregator.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly ReportService _reportService;

    public ReportsController(ReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("daily")]
    public async Task<ActionResult<DailyReportDto>> GetDailyReport()
    {
        return Ok(await _reportService.GetDailyReportAsync());
    }

    [HttpGet("runs")]
    public async Task<ActionResult<List<ScraperRunDto>>> GetRecentRuns([FromQuery] int take = 20)
    {
        return Ok(await _reportService.GetRecentRunsAsync(take));
    }
}
