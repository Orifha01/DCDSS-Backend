using DCDSS.Entities;

namespace DCDSS.Repositories.Interfaces;

public interface IPatientAllergyRepository
{
    Task<IList<PatientAllergy>> GetByPatientAsync(int patientId);
    Task DeleteByPatientAsync(int patientId);
    Task AddAsync(PatientAllergy allergy);
}
