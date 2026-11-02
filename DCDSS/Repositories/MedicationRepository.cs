using DCDSS.Data;
using DCDSS.Entities;
using DCDSS.Entities.Enums;
using DCDSS.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DCDSS.Repositories;

public class MedicationRepository : Repository<Medication>, IMedicationRepository
{
    public MedicationRepository(DcdssContext context) : base(context) { }

    public Task<IList<Medication>> GetActiveByPatientAsync(int patientId)
    {
        return Query()
            .Include(m => m.Record)
            .Where(m => m.Record.PatientId == patientId && m.Status == MedicationStatus.Active)
            .ToListAsync()
            .ContinueWith(t => (IList<Medication>)t.Result);
    }
}
