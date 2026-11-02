using DCDSS.DTOs.Common;
using DCDSS.DTOs.Patients;

namespace DCDSS.Services.Interfaces;

public interface IPatientService
{
    Task<PagedResult<PatientSummaryDto>> GetPagedAsync(int page, int pageSize, string? search);
    Task<PatientDetailDto?> GetByIdWithHistoryAsync(int id);
    Task<PatientDetailDto> CreateAsync(CreatePatientDto dto);
    Task<PatientDetailDto> UpdateAsync(int id, UpdatePatientDto dto);
}
