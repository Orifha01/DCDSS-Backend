using DCDSS.DTOs.MedicalRecords;
using DCDSS.Middleware;
using DCDSS.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        var result = await _service.CreateAsync(dto);
        return Created($"/api/medical-records/{result.RecordId}", result);
    }
}
