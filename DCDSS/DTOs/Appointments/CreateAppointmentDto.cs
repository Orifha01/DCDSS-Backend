using System.ComponentModel.DataAnnotations;

namespace DCDSS.DTOs.Appointments;

public class CreateAppointmentDto
{
    [Required]
    public int PatientId { get; set; }

    [Required]
    public int DoctorId { get; set; }

    [Required]
    public DateTime AppointmentDate { get; set; }
}
