using DCDSS.DTOs.Common;
using DCDSS.DTOs.Laboratory;
using DCDSS.DTOs.MedicalRecords;
using DCDSS.DTOs.Medications;
using DCDSS.DTOs.Patients;
using DCDSS.Entities;
using DCDSS.Exceptions;
using DCDSS.Repositories.Interfaces;
using DCDSS.Services.Interfaces;

namespace DCDSS.Services;

public class PatientService : IPatientService
{
    private readonly IPatientRepository _patientRepo;
    private readonly IPatientAllergyRepository _allergyRepo;
    private readonly IUnitOfWork _uow;

    public PatientService(
        IPatientRepository patientRepo,
        IPatientAllergyRepository allergyRepo,
        IUnitOfWork uow)
    {
        _patientRepo = patientRepo;
        _allergyRepo = allergyRepo;
        _uow = uow;
    }

    public async Task<PagedResult<PatientSummaryDto>> GetPagedAsync(int page, int pageSize, string? search)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, total) = await _patientRepo.SearchAsync(page, pageSize, search);

        return new PagedResult<PatientSummaryDto>
        {
            Items = items.Select(MapToSummary).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PatientDetailDto?> GetByIdWithHistoryAsync(int id)
    {
        var patient = await _patientRepo.GetByIdWithHistoryAsync(id);
        return patient == null ? null : MapToDetail(patient);
    }

    public async Task<PatientDetailDto> CreateAsync(CreatePatientDto dto)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (dto.DateOfBirth > today)
            throw new BusinessRuleException("Date of birth cannot be in the future.");

        var patient = new Patient
        {
            FirstName = dto.FirstName,
            Surname = dto.Surname,
            Gender = dto.Gender,
            DateOfBirth = dto.DateOfBirth,
            Address = dto.Address,
            Phone = dto.Phone,
            BloodType = dto.BloodType,
            EmergencyContact = dto.EmergencyContact,
            CreatedAt = DateTime.UtcNow
        };

        await _patientRepo.AddAsync(patient);
        await _uow.SaveChangesAsync();

        // Save allergy rows
        await SaveAllergiesAsync(patient.PatientId, dto.Allergies);

        // Reload with navigation data
        var loaded = await _patientRepo.GetByIdWithHistoryAsync(patient.PatientId);
        return MapToDetail(loaded!);
    }

    public async Task<PatientDetailDto> UpdateAsync(int id, UpdatePatientDto dto)
    {
        var patient = await _patientRepo.GetByIdAsync(id)
            ?? throw new NotFoundException($"Patient with ID {id} not found.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (dto.DateOfBirth > today)
            throw new BusinessRuleException("Date of birth cannot be in the future.");

        patient.FirstName = dto.FirstName;
        patient.Surname = dto.Surname;
        patient.Gender = dto.Gender;
        patient.DateOfBirth = dto.DateOfBirth;
        patient.Address = dto.Address;
        patient.Phone = dto.Phone;
        patient.BloodType = dto.BloodType;
        patient.EmergencyContact = dto.EmergencyContact;

        _patientRepo.Update(patient);

        // Replace allergy set
        await _allergyRepo.DeleteByPatientAsync(id);
        await SaveAllergiesAsync(id, dto.Allergies);

        await _uow.SaveChangesAsync();

        var loaded = await _patientRepo.GetByIdWithHistoryAsync(id);
        return MapToDetail(loaded!);
    }

    private async Task SaveAllergiesAsync(int patientId, IList<string>? allergies)
    {
        if (allergies == null || allergies.Count == 0) return;

        var normalized = allergies
            .Select(a => a.Trim())
            .Where(a => !string.IsNullOrEmpty(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var allergen in normalized)
        {
            await _allergyRepo.AddAsync(new PatientAllergy
            {
                PatientId = patientId,
                Allergen = allergen
            });
        }

        await _uow.SaveChangesAsync();
    }

    private static PatientSummaryDto MapToSummary(Patient p) => new()
    {
        PatientId = p.PatientId,
        FirstName = p.FirstName,
        Surname = p.Surname,
        Gender = p.Gender.ToString(),
        DateOfBirth = p.DateOfBirth,
        Phone = p.Phone,
        BloodType = p.BloodType.ToString(),
        CreatedAt = p.CreatedAt
    };

    private static PatientDetailDto MapToDetail(Patient p) => new()
    {
        PatientId = p.PatientId,
        FirstName = p.FirstName,
        Surname = p.Surname,
        Gender = p.Gender.ToString(),
        DateOfBirth = p.DateOfBirth,
        Address = p.Address,
        Phone = p.Phone,
        BloodType = p.BloodType.ToString(),
        EmergencyContact = p.EmergencyContact,
        CreatedAt = p.CreatedAt,
        Allergies = p.Allergies?.Select(a => a.Allergen).ToList() ?? [],
        MedicalHistory = p.MedicalRecords?.Select(MapRecord).ToList() ?? []
    };

    private static MedicalRecordDto MapRecord(Entities.MedicalRecord r) => new()
    {
        RecordId = r.RecordId,
        PatientId = r.PatientId,
        DoctorId = r.DoctorId,
        DoctorName = r.Doctor?.FullName,
        Diagnosis = r.Diagnosis,
        TreatmentNotes = r.TreatmentNotes,
        VisitDate = r.VisitDate,
        Status = r.Status.ToString(),
        LabResults = r.LabResults?.Select(MapLabResult).ToList() ?? [],
        Medications = r.Medications?.Select(MapMedication).ToList() ?? []
    };

    private static LabResultDto MapLabResult(Entities.LabResult l) => new()
    {
        ResultId = l.ResultId,
        RecordId = l.RecordId,
        TestName = l.TestName,
        ResultValue = l.ResultValue,
        ReferenceRange = l.ReferenceRange,
        Status = l.Status.ToString(),
        OrderedBy = l.OrderedBy,
        DateOrdered = l.DateOrdered
    };

    private static MedicationDto MapMedication(Entities.Medication m) => new()
    {
        MedicationId = m.MedicationId,
        RecordId = m.RecordId,
        DrugName = m.DrugName,
        Dosage = m.Dosage,
        Frequency = m.Frequency,
        StartDate = m.StartDate,
        Status = m.Status.ToString()
    };
}
