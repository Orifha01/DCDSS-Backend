using DCDSS.Entities;
using DCDSS.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCDSS.Data.Configurations;

public class MedicationConfiguration : IEntityTypeConfiguration<Medication>
{
    public void Configure(EntityTypeBuilder<Medication> builder)
    {
        builder.HasKey(m => m.MedicationId);
        builder.Property(m => m.MedicationId)
               .HasColumnName("MedicationID")
               .ValueGeneratedOnAdd();

        builder.Property(m => m.RecordId)
               .IsRequired()
               .HasColumnName("RecordID");

        builder.Property(m => m.DrugName)
               .IsRequired()
               .HasMaxLength(100)
               .HasColumnName("DrugName");

        builder.Property(m => m.Dosage)
               .IsRequired()
               .HasMaxLength(50)
               .HasColumnName("Dosage");

        builder.Property(m => m.Frequency)
               .IsRequired()
               .HasMaxLength(50)
               .HasColumnName("Frequency");

        builder.Property(m => m.StartDate)
               .IsRequired()
               .HasColumnName("StartDate")
               .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.Property(m => m.Status)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(20)
               .HasColumnName("Status")
               .HasDefaultValue(MedicationStatus.Active);

        builder.HasIndex(m => m.RecordId);
    }
}
