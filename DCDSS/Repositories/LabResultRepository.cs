using DCDSS.Data;
using DCDSS.Entities;
using DCDSS.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DCDSS.Repositories;

public class LabResultRepository : Repository<LabResult>, ILabResultRepository
{
    public LabResultRepository(DcdssContext context) : base(context) { }

    public Task<IList<LabResult>> GetByPatientAsync(int patientId)
    {
        return Query()
            .Include(lr => lr.Record)
            .Where(lr => lr.Record.PatientId == patientId)
            .OrderByDescending(lr => lr.DateOrdered)
            .ToListAsync()
            .ContinueWith(t => (IList<LabResult>)t.Result);
    }
}
