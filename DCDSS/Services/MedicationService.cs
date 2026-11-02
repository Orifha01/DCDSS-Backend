using DCDSS.DTOs.Medications;
using DCDSS.Entities;
using DCDSS.Entities.Enums;
using DCDSS.Exceptions;
using DCDSS.Models.Cds;
using DCDSS.Repositories.Interfaces;
using DCDSS.Services.Interfaces;

namespace DCDSS.Services;

public class MedicationService : IMedicationService
{
    private readonly IMedicationRepository _medicationRepo;
    private readonly IMedicalRecordRepository _recordRepo;
    private readonly ISafetyCheckService _safetyService;
    private readonly IUnitOfWork _uow;

    public MedicationService(
        IMedicationRepository medicationRepo,
        IMedicalRecordRepository recordRepo,
        ISafetyCheckService safetyService,
        IUnitOfWork uow)
    {
        _medicationRepo = medicationRepo;
        _recordRepo = recordRepo;
        _safetyService = safetyService;
        _uow = uow;
    }

    public async Task<PrescribeMedicationResponseDto> PrescribeAsync(CreateMedicationDto dto, int currentUserId)
    {
        var record = await _recordRepo.GetByIdAsync(dto.RecordId)
            ?? throw new NotFoundException($"Medical record with ID {dto.RecordId} not found.");

        int patientId = record.PatientId;

        var check = await _safetyService.CheckPrescriptionAsync(patientId, dto.DrugName, excludeMedicationId: null);

        if (!check.Allowed)
        {
            var criticalMessages = string.Join("; ", check.Conflicts
                .Where(c => c.Severity == CdsSeverity.Critical)
                .Select(c => c.Message));
            throw new ConflictException($"Prescription blocked due to critical safety conflicts: {criticalMessages}");
        }

        DateTime start = dto.StartDate ?? DateTime.UtcNow;
        if (dto.StartDate.HasValue && dto.StartDate.Value > DateTime.UtcNow)
            throw new BusinessRuleException("Start date cannot be in the future.");

        var medication = new Medication
        {
            RecordId = dto.RecordId,
            DrugName = dto.DrugName,
            Dosage = dto.Dosage,
            Frequency = dto.Frequency,
            StartDate = start,
            Status = MedicationStatus.Active
        };

        await _medicationRepo.AddAsync(medication);
        await _uow.SaveChangesAsync();

        var medDto = MapToDto(medication);

        var warnings = check.Conflicts
            .Where(c => c.Severity != CdsSeverity.Critical)
            .Select(c => new SafetyWarningDto
            {
                RuleId = c.RuleId,
                Type = c.Type.ToString(),
                Severity = c.Severity.ToString(),
                Message = c.Message,
                RelatedDrug = c.RelatedDrug,
                RelatedAllergen = c.RelatedAllergen
            })
            .ToList();

        return new PrescribeMedicationResponseDto
        {
            Medication = medDto,
            Warnings = warnings
        };
    }

    public async Task<MedicationDto> DiscontinueAsync(int id)
    {
        var medication = await _medicationRepo.GetByIdAsync(id)
            ?? throw new NotFoundException($"Medication with ID {id} not found.");

        medication.Status = MedicationStatus.Discontinued;
        _medicationRepo.Update(medication);
        await _uow.SaveChangesAsync();

        return MapToDto(medication);
    }

    private static MedicationDto MapToDto(Medication m) => new()
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
