using DCDSS.Entities.Enums;

namespace DCDSS.Entities;

public class Appointment
{
    public int AppointmentId { get; set; }
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public DateTime AppointmentDate { get; set; }
    public AppointmentStatus Status { get; set; }

    public Patient Patient { get; set; } = null!;
    public User Doctor { get; set; } = null!;
}
