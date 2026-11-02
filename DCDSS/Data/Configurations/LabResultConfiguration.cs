using DCDSS.Entities;
using DCDSS.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCDSS.Data.Configurations;

public class LabResultConfiguration : IEntityTypeConfiguration<LabResult>
{
    public void Configure(EntityTypeBuilder<LabResult> builder)
    {
        builder.HasKey(lr => lr.ResultId);
        builder.Property(lr => lr.ResultId)
               .HasColumnName("ResultID")
               .ValueGeneratedOnAdd();

        builder.Property(lr => lr.RecordId)
               .IsRequired()
               .HasColumnName("RecordID");

        builder.Property(lr => lr.TestName)
               .IsRequired()
               .HasMaxLength(100)
               .HasColumnName("TestName");

        builder.Property(lr => lr.ResultValue)
               .HasMaxLength(50)
               .HasColumnName("ResultValue");

        builder.Property(lr => lr.ReferenceRange)
               .IsRequired()
               .HasMaxLength(100)
               .HasColumnName("ReferenceRange");

        builder.Property(lr => lr.Status)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(20)
               .HasColumnName("Status")
               .HasDefaultValue(LabResultStatus.Ordered);

        builder.Property(lr => lr.OrderedBy)
               .IsRequired()
               .HasColumnName("OrderedBy");

        builder.Property(lr => lr.DateOrdered)
               .IsRequired()
               .HasColumnName("DateOrdered")
               .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(lr => lr.RecordId);
        builder.HasIndex(lr => lr.OrderedBy);
        builder.HasIndex(lr => lr.DateOrdered);
    }
}
