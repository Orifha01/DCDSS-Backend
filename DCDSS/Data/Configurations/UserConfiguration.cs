using DCDSS.Entities;
using DCDSS.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCDSS.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.UserId);
        builder.Property(u => u.UserId)
               .HasColumnName("UserID")
               .ValueGeneratedOnAdd();

        builder.Property(u => u.Username)
               .IsRequired()
               .HasMaxLength(50)
               .HasColumnName("Username");
        builder.HasIndex(u => u.Username).IsUnique();

        builder.Property(u => u.PasswordHash)
               .IsRequired()
               .HasMaxLength(512)
               .HasColumnName("PasswordHash");

        builder.Property(u => u.FullName)
               .IsRequired()
               .HasMaxLength(100)
               .HasColumnName("FullName");

        builder.Property(u => u.Email)
               .IsRequired()
               .HasMaxLength(150)
               .HasColumnName("Email");
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.Role)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(20)
               .HasColumnName("Role");

        builder.Property(u => u.Department)
               .HasMaxLength(100)
               .HasColumnName("Department");

        builder.Property(u => u.CreatedAt)
               .IsRequired()
               .HasColumnName("CreatedAt")
               .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasMany(u => u.AuthoredMedicalRecords)
               .WithOne(mr => mr.Doctor)
               .HasForeignKey(mr => mr.DoctorId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.OrderedLabResults)
               .WithOne(lr => lr.OrderedByUser)
               .HasForeignKey(lr => lr.OrderedBy)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.Appointments)
               .WithOne(a => a.Doctor)
               .HasForeignKey(a => a.DoctorId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.AuditLogs)
               .WithOne(al => al.User)
               .HasForeignKey(al => al.UserId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
