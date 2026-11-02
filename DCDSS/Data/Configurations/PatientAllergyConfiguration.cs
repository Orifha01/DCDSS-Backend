using DCDSS.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCDSS.Data.Configurations;

public class PatientAllergyConfiguration : IEntityTypeConfiguration<PatientAllergy>
{
    public void Configure(EntityTypeBuilder<PatientAllergy> builder)
    {
        builder.HasKey(pa => pa.PatientAllergyId);
        builder.Property(pa => pa.PatientAllergyId)
               .HasColumnName("PatientAllergyID")
               .ValueGeneratedOnAdd();

        builder.Property(pa => pa.PatientId)
               .IsRequired()
               .HasColumnName("PatientID");

        builder.Property(pa => pa.Allergen)
               .IsRequired()
               .HasMaxLength(100)
               .HasColumnName("Allergen");

        builder.HasIndex(pa => new { pa.PatientId, pa.Allergen }).IsUnique();
        builder.HasIndex(pa => pa.PatientId);

        builder.HasOne(pa => pa.Patient)
               .WithMany(p => p.Allergies)
               .HasForeignKey(pa => pa.PatientId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
