using DCDSS.DTOs.Medications;

namespace DCDSS.Services.Interfaces;

public interface IMedicationService
{
    Task<PrescribeMedicationResponseDto> PrescribeAsync(CreateMedicationDto dto, int currentUserId);
    Task<MedicationDto> DiscontinueAsync(int id);
}
