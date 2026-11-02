using DCDSS.Entities;
using DCDSS.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCDSS.Data.Configurations;

public class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.HasKey(p => p.PatientId);
        builder.Property(p => p.PatientId)
               .HasColumnName("PatientID")
               .ValueGeneratedOnAdd();

        builder.Property(p => p.FirstName)
               .IsRequired()
               .HasMaxLength(50)
               .HasColumnName("FirstName");

        builder.Property(p => p.Surname)
               .IsRequired()
               .HasMaxLength(50)
               .HasColumnName("Surname");

        builder.Property(p => p.Gender)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(10)
               .HasColumnName("Gender");

        builder.Property(p => p.DateOfBirth)
               .IsRequired()
               .HasColumnName("DOB");

        builder.Property(p => p.Address)
               .IsRequired()
               .HasMaxLength(200)
               .HasColumnName("Address");

        builder.Property(p => p.Phone)
               .IsRequired()
               .HasMaxLength(20)
               .HasColumnName("Phone");

        builder.Property(p => p.BloodType)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(12)
               .HasColumnName("BloodType");

        builder.Property(p => p.EmergencyContact)
               .HasMaxLength(100)
               .HasColumnName("EmergencyContact");

        builder.Property(p => p.CreatedAt)
               .IsRequired()
               .HasColumnName("CreatedAt")
               .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasMany(p => p.MedicalRecords)
               .WithOne(mr => mr.Patient)
               .HasForeignKey(mr => mr.PatientId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Appointments)
               .WithOne(a => a.Patient)
               .HasForeignKey(a => a.PatientId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Allergies)
               .WithOne(pa => pa.Patient)
               .HasForeignKey(pa => pa.PatientId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
