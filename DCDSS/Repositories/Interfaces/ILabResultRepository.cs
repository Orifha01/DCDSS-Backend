using DCDSS.Entities;
using DCDSS.Services.Interfaces;

namespace DCDSS.Repositories.Interfaces;

public interface ILabResultRepository : IRepository<LabResult>
{
    Task<IList<LabResult>> GetByPatientAsync(int patientId);
}
