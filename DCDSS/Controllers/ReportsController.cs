using DCDSS.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DCDSS.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("monthly-stats")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> GetMonthlyStats(
        [FromQuery] int? month = null,
        [FromQuery] int? year = null)
    {
        month ??= DateTime.UtcNow.Month;
        year ??= DateTime.UtcNow.Year;

        var result = await _reportService.GetMonthlyStatsAsync(month.Value, year.Value);
        return Ok(result);
    }
}
