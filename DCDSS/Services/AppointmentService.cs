using DCDSS.DTOs.Appointments;
using DCDSS.Entities;
using DCDSS.Entities.Enums;
using DCDSS.Exceptions;
using DCDSS.Repositories.Interfaces;
using DCDSS.Services.Interfaces;

namespace DCDSS.Services;

public class AppointmentService : IAppointmentService
{
    private readonly IAppointmentRepository _apptRepo;
    private readonly IRepository<Patient> _patientRepo;
    private readonly IRepository<User> _userRepo;
    private readonly IUnitOfWork _uow;

    public AppointmentService(
        IAppointmentRepository apptRepo,
        IRepository<Patient> patientRepo,
        IRepository<User> userRepo,
        IUnitOfWork uow)
    {
        _apptRepo = apptRepo;
        _patientRepo = patientRepo;
        _userRepo = userRepo;
        _uow = uow;
    }

    public async Task<AppointmentDto> CreateAsync(CreateAppointmentDto dto)
    {
        var patient = await _patientRepo.GetByIdAsync(dto.PatientId)
            ?? throw new NotFoundException($"Patient with ID {dto.PatientId} not found.");

        var doctor = await _userRepo.GetByIdAsync(dto.DoctorId)
            ?? throw new NotFoundException($"User with ID {dto.DoctorId} not found.");

        if (doctor.Role != UserRole.Doctor)
            throw new BusinessRuleException("Selected user is not a Doctor.");

        if (dto.AppointmentDate <= DateTime.UtcNow)
            throw new BusinessRuleException("Appointment date must be in the future.");

        var appointment = new Appointment
        {
            PatientId = dto.PatientId,
            DoctorId = dto.DoctorId,
            AppointmentDate = dto.AppointmentDate,
            Status = AppointmentStatus.Scheduled
        };

        await _apptRepo.AddAsync(appointment);
        await _uow.SaveChangesAsync();

        return new AppointmentDto
        {
            AppointmentId = appointment.AppointmentId,
            PatientId = appointment.PatientId,
            DoctorId = appointment.DoctorId,
            AppointmentDate = appointment.AppointmentDate,
            Status = appointment.Status.ToString(),
            PatientName = $"{patient.FirstName} {patient.Surname}",
            DoctorName = doctor.FullName
        };
    }

    public async Task<IList<AppointmentDto>> GetByDoctorAsync(int doctorId)
    {
        var doctor = await _userRepo.GetByIdAsync(doctorId)
            ?? throw new NotFoundException($"User with ID {doctorId} not found.");

        if (doctor.Role != UserRole.Doctor)
            throw new BusinessRuleException("Selected user is not a Doctor.");

        var appointments = await _apptRepo.GetByDoctorAsync(doctorId);

        return appointments.Select(a => new AppointmentDto
        {
            AppointmentId = a.AppointmentId,
            PatientId = a.PatientId,
            DoctorId = a.DoctorId,
            AppointmentDate = a.AppointmentDate,
            Status = a.Status.ToString(),
            PatientName = a.Patient != null ? $"{a.Patient.FirstName} {a.Patient.Surname}" : null,
            DoctorName = a.Doctor?.FullName
        }).ToList();
    }
}
