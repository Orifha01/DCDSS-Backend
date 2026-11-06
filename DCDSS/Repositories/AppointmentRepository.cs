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

    public Task<bool> HasConflictAsync(int doctorId, int patientId, DateTime appointmentDate)
    {
        // Block if the same doctor already has a Scheduled appointment
        // within a 30-minute window of the requested time
        var windowStart = appointmentDate.AddMinutes(-30);
        var windowEnd = appointmentDate.AddMinutes(30);

        return Query()
            .AnyAsync(a =>
                a.Status == Entities.Enums.AppointmentStatus.Scheduled &&
                a.AppointmentDate >= windowStart &&
                a.AppointmentDate <= windowEnd &&
                (a.DoctorId == doctorId || a.PatientId == patientId));
    }
}
