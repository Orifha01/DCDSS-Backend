using DCDSS.DTOs.Appointments;
using DCDSS.Middleware;
using DCDSS.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DCDSS.Controllers;

[ApiController]
[Route("api/appointments")]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _service;

    public AppointmentsController(IAppointmentService service)
    {
        _service = service;
    }

    [HttpPost]
    [Authorize(Roles = "Doctor,Nurse,Administrator")]
    [AuditAction("CREATE_APPOINTMENT")]
    public async Task<IActionResult> Create([FromBody] CreateAppointmentDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return Created($"/api/appointments/{result.AppointmentId}", result);
    }

    [HttpGet("doctor/{doctorId:int}")]
    [Authorize(Roles = "Doctor,Nurse,Administrator")]
    public async Task<IActionResult> GetByDoctor(int doctorId)
    {
        var results = await _service.GetByDoctorAsync(doctorId);
        return Ok(results);
    }
}
