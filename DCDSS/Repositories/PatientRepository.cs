using DCDSS.Data;
using DCDSS.Entities;
using DCDSS.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DCDSS.Repositories;

public class PatientRepository : Repository<Patient>, IPatientRepository
{
    public PatientRepository(DcdssContext context) : base(context) { }

    public async Task<(IList<Patient> Items, int Total)> SearchAsync(int page, int pageSize, string? search)
    {
        var q = Query();

        if (!string.IsNullOrWhiteSpace(search))
        {
            q = q.Where(p => p.FirstName.Contains(search) || p.Surname.Contains(search));
        }

        int total = await q.CountAsync();
        var items = await q
            .OrderBy(p => p.Surname)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public Task<Patient?> GetByIdWithHistoryAsync(int id)
    {
        return Query()
            .Include(p => p.Allergies)
            .Include(p => p.MedicalRecords)
                .ThenInclude(mr => mr.LabResults)
            .Include(p => p.MedicalRecords)
                .ThenInclude(mr => mr.Medications)
            .Include(p => p.MedicalRecords)
                .ThenInclude(mr => mr.Doctor)
            .FirstOrDefaultAsync(p => p.PatientId == id);
    }
}
