using EstateAggregator.DTOs;
using EstateAggregator.Services;
using Microsoft.AspNetCore.Mvc;

namespace EstateAggregator.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppSettingsController : ControllerBase
{
    private readonly AppSettingsService _appSettingsService;

    public AppSettingsController(AppSettingsService appSettingsService)
    {
        _appSettingsService = appSettingsService;
    }

    [HttpGet]
    public async Task<ActionResult<AppSettingDto>> GetSettings()
    {
        var settings = await _appSettingsService.GetAsync();
        if (settings == null)
            return NotFound();

        return Ok(settings);
    }

    [HttpPut]
    public async Task<ActionResult<AppSettingDto>> UpdateSettings([FromBody] UpdateAppSettingDto dto)
    {
        var updated = await _appSettingsService.UpdateAsync(dto);
        if (updated == null)
            return NotFound();

        return Ok(updated);
    }
}
