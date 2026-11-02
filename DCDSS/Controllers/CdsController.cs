using DCDSS.DTOs.Cds;
using DCDSS.Middleware;
using DCDSS.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DCDSS.Controllers;

[ApiController]
[Route("api/cds")]
public class CdsController : ControllerBase
{
    private readonly ICdsEngine _cdsEngine;

    public CdsController(ICdsEngine cdsEngine)
    {
        _cdsEngine = cdsEngine;
    }

    [HttpPost("evaluate")]
    [Authorize(Roles = "Doctor,Nurse")]
    [AuditAction("CDS_EVALUATE")]
    public async Task<IActionResult> Evaluate([FromBody] CdsEvaluationRequestDto dto)
    {
        var result = await _cdsEngine.EvaluateAsync(dto);
        return Ok(result);
    }
}
