namespace DCDSS.Middleware;

[AttributeUsage(AttributeTargets.Method)]
public sealed class AuditActionAttribute : Attribute
{
    public string ActionName { get; }
    public string? IdArgument { get; }

    public AuditActionAttribute(string actionName, string? idArgument = null)
    {
        ActionName = actionName;
        IdArgument = idArgument;
    }
}
