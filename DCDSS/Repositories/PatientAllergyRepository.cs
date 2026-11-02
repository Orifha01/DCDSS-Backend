using DCDSS.Data;
using DCDSS.Entities;
using DCDSS.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DCDSS.Repositories;

public class PatientAllergyRepository : IPatientAllergyRepository
{
    private readonly DcdssContext _context;

    public PatientAllergyRepository(DcdssContext context)
    {
        _context = context;
    }

    public Task<IList<PatientAllergy>> GetByPatientAsync(int patientId)
    {
        return _context.PatientAllergies
            .Where(pa => pa.PatientId == patientId)
            .ToListAsync()
            .ContinueWith(t => (IList<PatientAllergy>)t.Result);
    }

    public async Task DeleteByPatientAsync(int patientId)
    {
        var rows = await _context.PatientAllergies
            .Where(pa => pa.PatientId == patientId)
            .ToListAsync();
        _context.PatientAllergies.RemoveRange(rows);
    }

    public async Task AddAsync(PatientAllergy allergy)
    {
        await _context.PatientAllergies.AddAsync(allergy);
    }
}
