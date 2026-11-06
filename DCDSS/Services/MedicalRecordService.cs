using DCDSS.DTOs.Laboratory;
using DCDSS.DTOs.MedicalRecords;
using DCDSS.DTOs.Medications;
using DCDSS.Entities;
using DCDSS.Entities.Enums;
using DCDSS.Exceptions;
using DCDSS.Repositories.Interfaces;
using DCDSS.Services.Interfaces;

namespace DCDSS.Services;

public class MedicalRecordService : IMedicalRecordService
{
    private readonly IMedicalRecordRepository _recordRepo;
    private readonly IRepository<Patient> _patientRepo;
    private readonly IRepository<User> _userRepo;
    private readonly IUnitOfWork _uow;

    public MedicalRecordService(
        IMedicalRecordRepository recordRepo,
        IRepository<Patient> patientRepo,
        IRepository<User> userRepo,
        IUnitOfWork uow)
    {
        _recordRepo = recordRepo;
        _patientRepo = patientRepo;
        _userRepo = userRepo;
        _uow = uow;
    }

    public async Task<MedicalRecordDto> CreateAsync(CreateMedicalRecordDto dto)
    {
        var patient = await _patientRepo.GetByIdAsync(dto.PatientId)
            ?? throw new NotFoundException($"Patient with ID {dto.PatientId} not found.");

        var doctor = await _userRepo.GetByIdAsync(dto.DoctorId)
            ?? throw new NotFoundException($"User with ID {dto.DoctorId} not found.");

        if (doctor.Role != UserRole.Doctor && doctor.Role != UserRole.Nurse)
            throw new BusinessRuleException("Selected user is not a Doctor or Nurse.");

        var record = new MedicalRecord
        {
            PatientId = dto.PatientId,
            DoctorId = dto.DoctorId,
            Diagnosis = dto.Diagnosis,
            TreatmentNotes = dto.TreatmentNotes,
            VisitDate = dto.VisitDate,
            Status = MedicalRecordStatus.Active
        };

        await _recordRepo.AddAsync(record);
        await _uow.SaveChangesAsync();

        return new MedicalRecordDto
        {
            RecordId = record.RecordId,
            PatientId = record.PatientId,
            DoctorId = record.DoctorId,
            DoctorName = doctor.FullName,
            Diagnosis = record.Diagnosis,
            TreatmentNotes = record.TreatmentNotes,
            VisitDate = record.VisitDate,
            Status = record.Status.ToString(),
            LabResults = [],
            Medications = []
        };
    }
}
