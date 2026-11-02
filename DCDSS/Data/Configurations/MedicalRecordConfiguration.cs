using DCDSS.Entities;
using DCDSS.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCDSS.Data.Configurations;

public class MedicalRecordConfiguration : IEntityTypeConfiguration<MedicalRecord>
{
    public void Configure(EntityTypeBuilder<MedicalRecord> builder)
    {
        builder.HasKey(mr => mr.RecordId);
        builder.Property(mr => mr.RecordId)
               .HasColumnName("RecordID")
               .ValueGeneratedOnAdd();

        builder.Property(mr => mr.PatientId)
               .IsRequired()
               .HasColumnName("PatientID");

        builder.Property(mr => mr.DoctorId)
               .IsRequired()
               .HasColumnName("DoctorID");

        builder.Property(mr => mr.Diagnosis)
               .IsRequired()
               .HasMaxLength(500)
               .HasColumnName("Diagnosis");

        builder.Property(mr => mr.TreatmentNotes)
               .HasMaxLength(2000)
               .HasColumnName("TreatmentNotes");

        builder.Property(mr => mr.VisitDate)
               .IsRequired()
               .HasColumnName("VisitDate");

        builder.Property(mr => mr.Status)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(20)
               .HasColumnName("Status")
               .HasDefaultValue(MedicalRecordStatus.Active);

        builder.HasIndex(mr => mr.PatientId);
        builder.HasIndex(mr => mr.DoctorId);
        builder.HasIndex(mr => mr.VisitDate);

        builder.HasMany(mr => mr.LabResults)
               .WithOne(lr => lr.Record)
               .HasForeignKey(lr => lr.RecordId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(mr => mr.Medications)
               .WithOne(m => m.Record)
               .HasForeignKey(m => m.RecordId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
