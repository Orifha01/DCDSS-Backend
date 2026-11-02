using DCDSS.DTOs.Audit;
using DCDSS.DTOs.Common;
using DCDSS.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DCDSS.Controllers;

[ApiController]
[Route("api/audit")]
public class AuditController : ControllerBase
{
    private readonly IAuditLogRepository _auditRepo;

    public AuditController(IAuditLogRepository auditRepo)
    {
        _auditRepo = auditRepo;
    }

    [HttpGet]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, total) = await _auditRepo.GetPagedAsync(page, pageSize);

        var mapped = items.Select(al => new AuditLogDto
        {
            AuditId = al.AuditId,
            UserId = al.UserId,
            Username = al.User?.Username,
            Action = al.Action,
            Details = al.Details,
            Timestamp = al.Timestamp
        }).ToList();

        return Ok(new PagedResult<AuditLogDto>
        {
            Items = mapped,
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }
}
