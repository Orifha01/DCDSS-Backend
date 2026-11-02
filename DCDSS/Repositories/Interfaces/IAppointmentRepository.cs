using DCDSS.Entities;
using DCDSS.Services.Interfaces;

namespace DCDSS.Repositories.Interfaces;

public interface IAppointmentRepository : IRepository<Appointment>
{
    Task<IList<Appointment>> GetByDoctorAsync(int doctorId);
}
