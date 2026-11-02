using DCDSS.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCDSS.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(al => al.AuditId);
        builder.Property(al => al.AuditId)
               .HasColumnName("AuditID")
               .ValueGeneratedOnAdd();

        builder.Property(al => al.UserId)
               .HasColumnName("UserID");

        builder.Property(al => al.Action)
               .IsRequired()
               .HasMaxLength(50)
               .HasColumnName("Action");

        builder.Property(al => al.Details)
               .HasMaxLength(1000)
               .HasColumnName("Details");

        builder.Property(al => al.Timestamp)
               .IsRequired()
               .HasColumnName("Timestamp")
               .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(al => al.UserId);
        builder.HasIndex(al => al.Timestamp);
    }
}
