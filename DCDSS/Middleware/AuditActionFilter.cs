using System.Text.Json;
using DCDSS.DTOs.Cds;
using DCDSS.DTOs.Common;
using DCDSS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DCDSS.Middleware;

public class AuditActionFilter : IAsyncActionFilter
{
    private readonly IAuditLogService _auditService;
    private readonly ICurrentUserService _currentUser;

    public AuditActionFilter(IAuditLogService auditService, ICurrentUserService currentUser)
    {
        _auditService = auditService;
        _currentUser = currentUser;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // 1. Check for AuditActionAttribute
        var attr = context.ActionDescriptor.EndpointMetadata
            .OfType<AuditActionAttribute>()
            .FirstOrDefault();

        if (attr is null)
        {
            await next();
            return;
        }

        // 2. Capture user id before execution
        int? userId = _currentUser.GetUserId();

        // 3. Execute action with try/finally
        ActionExecutedContext executedCtx = null!;
        try
        {
            executedCtx = await next();
        }
        finally
        {
            if (executedCtx != null)
            {
                await WriteAuditAsync(context, executedCtx, attr, userId);
            }
        }
    }

    private async Task WriteAuditAsync(
        ActionExecutingContext context,
        ActionExecutedContext executedCtx,
        AuditActionAttribute attr,
        int? userId)
    {
        bool success = executedCtx.Exception == null || executedCtx.ExceptionHandled;
        string effectiveAction = (attr.ActionName == "LOGIN" && !success) ? "LOGIN_FAILED" : attr.ActionName;

        Dictionary<string, object?> details;

        if (attr.ActionName == "LOGIN" || attr.ActionName == "LOGIN_FAILED")
        {
            var loginDto = context.ActionArguments.Values.OfType<DTOs.Auth.LoginDto>().FirstOrDefault();
            details = new Dictionary<string, object?>
            {
                ["username"] = loginDto?.Username,
                ["success"] = success
            };
        }
        else if (attr.ActionName == "CDS_EVALUATE")
        {
            var cdsDto = context.ActionArguments.Values.OfType<CdsEvaluationRequestDto>().FirstOrDefault();
            details = new Dictionary<string, object?>
            {
                ["patientId"] = cdsDto?.PatientId,
                ["success"] = success
            };
        }
        else if (attr.IdArgument != null && context.ActionArguments.TryGetValue(attr.IdArgument, out var idVal))
        {
            details = new Dictionary<string, object?>
            {
                ["entityId"] = idVal,
                ["success"] = success
            };
        }
        else
        {
            // POST create — try to extract entity id from result
            int? entityId = null;
            if (executedCtx.Result is ObjectResult objResult && objResult.Value is IAuditableResult auditable)
            {
                entityId = auditable.EntityId;
            }
            details = new Dictionary<string, object?>
            {
                ["entityId"] = entityId,
                ["success"] = success
            };
        }

        string detailsJson = JsonSerializer.Serialize(details, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await _auditService.LogAsync(userId, effectiveAction, detailsJson);
    }
}
