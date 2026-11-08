using Microsoft.EntityFrameworkCore; // EF core
using SchoolAdmission.Api.Models; // entity

namespace SchoolAdmission.Api.Data; // data namespace

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options) // DB context
{
    public DbSet<ClassRoom> Classes => Set<ClassRoom>(); // Classes table
    public DbSet<Hold> Holds => Set<Hold>(); // Holds table

    protected override void OnModelCreating(ModelBuilder b) // table rules
    {
        b.Entity<ClassRoom>(e => // class rules
        {
            e.HasKey(x => x.Id); // PK
            e.Property(x => x.Id).HasMaxLength(40); // id length
            e.Property(x => x.Name).HasMaxLength(100).IsRequired(); // nam must
            e.ToTable(t => t.HasCheckConstraint("CK_Classes_TotalSeats", "\"TotalSeats\" >= 1")); // seat 1-er kom hobe na
        });

        b.Entity<Hold>(e => // hold rules
        {
            e.HasKey(x => x.Id); // PK
            e.Property(x => x.Id).HasMaxLength(40); // id length
            e.Property(x => x.ClassId).HasMaxLength(40); // class id length
            e.Property(x => x.StudentId).HasMaxLength(17).IsRequired(); // 17 ongko
            e.HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId); // class FK
            e.HasIndex(x => new { x.ClassId, x.Status, x.ExpiresAt }); // seat count fast
            e.HasIndex(x => x.StudentId); // /me/holds fast
            e.HasIndex(x => new { x.StudentId, x.ClassId }).IsUnique().HasFilter("\"Status\" IN (0, 1)").HasDatabaseName("UX_Holds_ActivePerStudentClass"); // DB level: ek student ek class-e ekta-i HELD/CONFIRMED
        });
    }
}