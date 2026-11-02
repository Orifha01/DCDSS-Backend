using DCDSS.DTOs.Appointments;

namespace DCDSS.Services.Interfaces;

public interface IAppointmentService
{
    Task<AppointmentDto> CreateAsync(CreateAppointmentDto dto);
    Task<IList<AppointmentDto>> GetByDoctorAsync(int doctorId);
}
