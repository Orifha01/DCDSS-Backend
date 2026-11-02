namespace DCDSS.Services.Interfaces;

public interface ICurrentUserService
{
    int? GetUserId();
    string? GetRole();
    string? GetUsername();
}
