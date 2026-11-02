using DCDSS.Entities;
using DCDSS.Services.Interfaces;

namespace DCDSS.Repositories.Interfaces;

public interface IPatientRepository : IRepository<Patient>
{
    Task<(IList<Patient> Items, int Total)> SearchAsync(int page, int pageSize, string? search);
    Task<Patient?> GetByIdWithHistoryAsync(int id);
}
