using DCDSS.DTOs.MedicalRecords;

namespace DCDSS.Services.Interfaces;

public interface IMedicalRecordService
{
    Task<MedicalRecordDto> CreateAsync(CreateMedicalRecordDto dto);
}
