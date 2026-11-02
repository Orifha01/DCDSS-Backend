using DCDSS.DTOs.Laboratory;
using DCDSS.Middleware;
using DCDSS.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DCDSS.Controllers;

[ApiController]
[Route("api/laboratory")]
public class LaboratoryController : ControllerBase
{
    private readonly ILabService _labService;
    private readonly ICurrentUserService _currentUser;

    public LaboratoryController(ILabService labService, ICurrentUserService currentUser)
    {
        _labService = labService;
        _currentUser = currentUser;
    }

    [HttpPost("order")]
    [Authorize(Roles = "Doctor,Nurse")]
    [AuditAction("CREATE_LAB_ORDER")]
    public async Task<IActionResult> Order([FromBody] OrderLabTestDto dto)
    {
        int userId = _currentUser.GetUserId() ?? 0;
        var result = await _labService.OrderAsync(dto, userId);
        return Created($"/api/laboratory/results/{result.ResultId}", result);
    }

    [HttpPut("results/{id:int}")]
    [Authorize(Roles = "LaboratoryStaff")]
    [AuditAction("UPDATE_LAB_RESULT", "id")]
    public async Task<IActionResult> UpdateResult(int id, [FromBody] UpdateLabResultDto dto)
    {
        var result = await _labService.UpdateResultAsync(id, dto);
        return Ok(result);
    }

    [HttpGet("patient/{patientId:int}")]
    [Authorize(Roles = "Doctor,Nurse,LaboratoryStaff")]
    [AuditAction("VIEW_LAB_HISTORY", "patientId")]
    public async Task<IActionResult> GetByPatient(int patientId)
    {
        var results = await _labService.GetByPatientAsync(patientId);
        return Ok(results);
    }
}
