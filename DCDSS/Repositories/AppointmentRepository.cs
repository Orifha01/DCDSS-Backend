using DCDSS.Data;
using DCDSS.Entities;
using DCDSS.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DCDSS.Repositories;

public class AppointmentRepository : Repository<Appointment>, IAppointmentRepository
{
    public AppointmentRepository(DcdssContext context) : base(context) { }

    public Task<IList<Appointment>> GetByDoctorAsync(int doctorId)
    {
        return Query()
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Where(a => a.DoctorId == doctorId)
            .OrderBy(a => a.AppointmentDate)
            .ToListAsync()
            .ContinueWith(t => (IList<Appointment>)t.Result);
    }
}
