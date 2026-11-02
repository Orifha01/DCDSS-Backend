using DCDSS.DTOs.Laboratory;

namespace DCDSS.Services.Interfaces;

public interface ILabService
{
    Task<LabResultDto> OrderAsync(OrderLabTestDto dto, int orderedByUserId);
    Task<LabResultDto> UpdateResultAsync(int id, UpdateLabResultDto dto);
    Task<IList<LabResultDto>> GetByPatientAsync(int patientId);
}
