using DCDSS.DTOs.MedicalRecords;
using DCDSS.Middleware;
using DCDSS.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DCDSS.Controllers;

[ApiController]
[Route("api/medical-records")]
public class MedicalRecordsController : ControllerBase
{
    private readonly IMedicalRecordService _service;

    public MedicalRecordsController(IMedicalRecordService service)
    {
        _service = service;
    }

    [HttpPost]
    [Authorize(Roles = "Doctor,Nurse")]
    [AuditAction("CREATE_MEDICAL_RECORD")]
    public async Task<IActionResult> Create([FromBody] CreateMedicalRecordDto dto)
    {
        // Inject doctor ID from JWT token so the caller doesn't need to supply it
        var userIdClaim = User.FindFirst("sub")?.Value
                       ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var doctorId) || doctorId == 0)
            return Unauthorized(new { detail = "Could not determine authenticated user." });

        dto.DoctorId = doctorId;

        var result = await _service.CreateAsync(dto);
        return Created($"/api/medical-records/{result.RecordId}", result);
    }
}
