using DCDSS.DTOs.Laboratory;
using DCDSS.Entities;
using DCDSS.Entities.Enums;
using DCDSS.Exceptions;
using DCDSS.Repositories.Interfaces;
using DCDSS.Services.Interfaces;

namespace DCDSS.Services;

public class LabService : ILabService
{
    private readonly ILabResultRepository _labRepo;
    private readonly IMedicalRecordRepository _recordRepo;
    private readonly IUnitOfWork _uow;
    private readonly IReferenceRangeParser _parser;

    public LabService(
        ILabResultRepository labRepo,
        IMedicalRecordRepository recordRepo,
        IUnitOfWork uow,
        IReferenceRangeParser parser)
    {
        _labRepo = labRepo;
        _recordRepo = recordRepo;
        _uow = uow;
        _parser = parser;
    }

    public async Task<LabResultDto> OrderAsync(OrderLabTestDto dto, int orderedByUserId)
    {
        var record = await _recordRepo.GetByIdAsync(dto.RecordId)
            ?? throw new NotFoundException($"Medical record with ID {dto.RecordId} not found.");

        var labResult = new LabResult
        {
            RecordId = dto.RecordId,
            TestName = dto.TestName,
            ReferenceRange = dto.ReferenceRange,
            Status = LabResultStatus.Ordered,
            OrderedBy = orderedByUserId,
            DateOrdered = DateTime.UtcNow,
            ResultValue = null
        };

        await _labRepo.AddAsync(labResult);
        await _uow.SaveChangesAsync();

        return MapToDto(labResult);
    }

    public async Task<LabResultDto> UpdateResultAsync(int id, UpdateLabResultDto dto)
    {
        var labResult = await _labRepo.GetByIdAsync(id)
            ?? throw new NotFoundException($"Lab result with ID {id} not found.");

        labResult.ResultValue = dto.ResultValue;

        if (!string.IsNullOrEmpty(dto.ReferenceRange))
            labResult.ReferenceRange = dto.ReferenceRange;

        labResult.Status = _parser.Classify(labResult.ResultValue, labResult.ReferenceRange);

        _labRepo.Update(labResult);
        await _uow.SaveChangesAsync();

        return MapToDto(labResult);
    }

    public async Task<IList<LabResultDto>> GetByPatientAsync(int patientId)
    {
        var results = await _labRepo.GetByPatientAsync(patientId);
        return results.Select(MapToDto).ToList();
    }

    private static LabResultDto MapToDto(LabResult l) => new()
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
}
