using DCDSS.Entities;
using DCDSS.Services.Interfaces;

namespace DCDSS.Repositories.Interfaces;

public interface IMedicationRepository : IRepository<Medication>
{
    Task<IList<Medication>> GetActiveByPatientAsync(int patientId);
}
