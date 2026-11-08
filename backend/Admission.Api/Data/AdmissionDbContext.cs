using Admission.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Admission.Api.Data;

public sealed class AdmissionDbContext(DbContextOptions<AdmissionDbContext> options) : DbContext(options)
{
    public DbSet<AdmissionClass> Classes => Set<AdmissionClass>();

    public DbSet<Hold> Holds => Set<Hold>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdmissionClass>(entity =>
        {
            entity.ToTable("classes", table =>
            {
                table.HasCheckConstraint("ck_classes_name_not_blank", "btrim(name) <> ''");
                table.HasCheckConstraint("ck_classes_total_seats", "total_seats BETWEEN 1 AND 10000");
            });

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(x => x.TotalSeats).HasColumnName("total_seats").IsRequired();
            entity.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamptz")
                .HasDefaultValueSql("now()")
                .IsRequired();

            entity.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_classes_created_at");
        });

        modelBuilder.Entity<Hold>(entity =>
        {
            entity.ToTable("holds", table =>
            {
                table.HasCheckConstraint("ck_holds_student_id", "student_id ~ '^[0-9]{17}$'");
                table.HasCheckConstraint(
                    "ck_holds_status",
                    "status IN ('HELD','CONFIRMED','EXPIRED','CANCELLED')");
            });

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ClassId).HasColumnName("class_id").IsRequired();
            entity.Property(x => x.StudentId).HasColumnName("student_id").HasMaxLength(17).IsRequired();
            entity.Property(x => x.Status)
                .HasColumnName("status")
                .HasMaxLength(16)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamptz")
                .HasDefaultValueSql("now()")
                .IsRequired();
            entity.Property(x => x.ExpiresAt)
                .HasColumnName("expires_at")
                .HasColumnType("timestamptz")
                .IsRequired();
            entity.Property(x => x.ConfirmedAt).HasColumnName("confirmed_at").HasColumnType("timestamptz");
            entity.Property(x => x.EndedAt).HasColumnName("ended_at").HasColumnType("timestamptz");

            entity.HasOne(x => x.Class)
                .WithMany(x => x.Holds)
                .HasForeignKey(x => x.ClassId)
                .OnDelete(DeleteBehavior.Cascade);

            // Occupancy lookups for one class.
            entity.HasIndex(x => new { x.ClassId, x.Status })
                .HasDatabaseName("ix_holds_class_status")
                .IncludeProperties(x => x.ExpiresAt);

            // "My applications".
            entity.HasIndex(x => new { x.StudentId, x.CreatedAt })
                .HasDatabaseName("ix_holds_student");

            // Expiry sweeper.
            entity.HasIndex(x => x.ExpiresAt)
                .HasDatabaseName("ix_holds_expiring")
                .HasFilter("status = 'HELD'");

            // Hard guarantee: at most one active/confirmed hold per student per class.
            entity.HasIndex(x => new { x.ClassId, x.StudentId })
                .HasDatabaseName("ux_holds_one_active_per_class_student")
                .IsUnique()
                .HasFilter("status IN ('HELD','CONFIRMED')");
        });
    }
}
