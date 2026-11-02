using DCDSS.DTOs.Medications;
using DCDSS.Middleware;
using DCDSS.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DCDSS.Controllers;

[ApiController]
[Route("api/medications")]
public class MedicationsController : ControllerBase
{
    private readonly IMedicationService _service;
    private readonly ICurrentUserService _currentUser;

    public MedicationsController(IMedicationService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpPost]
    [Authorize(Roles = "Doctor")]
    [AuditAction("CREATE_MEDICATION")]
    public async Task<IActionResult> Prescribe([FromBody] CreateMedicationDto dto)
    {
        int userId = _currentUser.GetUserId() ?? 0;
        var result = await _service.PrescribeAsync(dto, userId);
        return Created($"/api/medications/{result.Medication.MedicationId}", result);
    }

    [HttpPut("{id:int}/discontinue")]
    [Authorize(Roles = "Doctor")]
    [AuditAction("DISCONTINUE_MEDICATION", "id")]
    public async Task<IActionResult> Discontinue(int id)
    {
        var result = await _service.DiscontinueAsync(id);
        return Ok(result);
    }
}
