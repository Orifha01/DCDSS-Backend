using DCDSS.Entities;
using DCDSS.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCDSS.Data.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.HasKey(a => a.AppointmentId);
        builder.Property(a => a.AppointmentId)
               .HasColumnName("AppointmentID")
               .ValueGeneratedOnAdd();

        builder.Property(a => a.PatientId)
               .IsRequired()
               .HasColumnName("PatientID");

        builder.Property(a => a.DoctorId)
               .IsRequired()
               .HasColumnName("DoctorID");

        builder.Property(a => a.AppointmentDate)
               .IsRequired()
               .HasColumnName("AppointmentDate");

        builder.Property(a => a.Status)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(20)
               .HasColumnName("Status")
               .HasDefaultValue(AppointmentStatus.Scheduled);

        builder.HasIndex(a => a.PatientId);
        builder.HasIndex(a => a.DoctorId);
        builder.HasIndex(a => a.AppointmentDate);
    }
}
