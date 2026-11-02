using DCDSS.DTOs.Patients;
using DCDSS.Middleware;
using DCDSS.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DCDSS.Controllers;

[ApiController]
[Route("api/patients")]
public class PatientsController : ControllerBase
{
    private readonly IPatientService _service;

    public PatientsController(IPatientService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Roles = "Doctor,Nurse,LaboratoryStaff")]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        var result = await _service.GetPagedAsync(page, pageSize, search);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Doctor,Nurse")]
    [AuditAction("CREATE_PATIENT")]
    public async Task<IActionResult> Create([FromBody] CreatePatientDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return Created($"/api/patients/{result.PatientId}", result);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Doctor,Nurse,LaboratoryStaff")]
    [AuditAction("VIEW_PATIENT", "id")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdWithHistoryAsync(id);
        if (result is null)
            return NotFound();
        return Ok(result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Doctor,Nurse")]
    [AuditAction("UPDATE_PATIENT", "id")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePatientDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        return Ok(result);
    }
}
