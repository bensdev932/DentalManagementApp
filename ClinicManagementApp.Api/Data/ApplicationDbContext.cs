using ClinicManagementApp.Api.Domain.Common;
using ClinicManagementApp.Api.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementApp.Api.Data;

/// <summary>
/// Entity Framework Core database context integrating ASP.NET Core Identity and all Clinic Management entities.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<PatientEntity> Patients => Set<PatientEntity>();
    public DbSet<AppointmentEntity> Appointments => Set<AppointmentEntity>();
    public DbSet<DentalTreatmentEntity> DentalTreatments => Set<DentalTreatmentEntity>();
    public DbSet<OrthodonticContractEntity> OrthodonticContracts => Set<OrthodonticContractEntity>();
    public DbSet<OrthodonticPaymentEntity> OrthodonticPayments => Set<OrthodonticPaymentEntity>();
    public DbSet<ExpenseEntity> Expenses => Set<ExpenseEntity>();
    public DbSet<ProcedureCatalogEntity> Procedures => Set<ProcedureCatalogEntity>();
    public DbSet<OutboxSyncRecordEntity> OutboxSyncRecords => Set<OutboxSyncRecordEntity>();
    public DbSet<ClientLogEntryEntity> ClientLogEntries => Set<ClientLogEntryEntity>();
    public DbSet<PatientOutboxTaskEntity> PatientOutboxTasks => Set<PatientOutboxTaskEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Global Query Filters for Soft Deletion
        builder.Entity<PatientEntity>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<AppointmentEntity>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<DentalTreatmentEntity>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<OrthodonticContractEntity>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<OrthodonticPaymentEntity>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<ExpenseEntity>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<ProcedureCatalogEntity>().HasQueryFilter(e => !e.IsDeleted);

        // Decimal precisions for financial values (PHP Currency)
        builder.Entity<PatientEntity>()
            .Property(p => p.ConsultationFee)
            .HasPrecision(18, 2);

        builder.Entity<DentalTreatmentEntity>()
            .Property(t => t.ConsultationFee)
            .HasPrecision(18, 2);
        builder.Entity<DentalTreatmentEntity>()
            .Property(t => t.TotalContractPrice)
            .HasPrecision(18, 2);
        builder.Entity<DentalTreatmentEntity>()
            .Property(t => t.DownPayment)
            .HasPrecision(18, 2);

        builder.Entity<OrthodonticContractEntity>()
            .Property(c => c.TotalContractPrice)
            .HasPrecision(18, 2);
        builder.Entity<OrthodonticContractEntity>()
            .Property(c => c.DownPayment)
            .HasPrecision(18, 2);

        builder.Entity<OrthodonticPaymentEntity>()
            .Property(p => p.Amount)
            .HasPrecision(18, 2);

        builder.Entity<ExpenseEntity>()
            .Property(e => e.Amount)
            .HasPrecision(18, 2);

        builder.Entity<ProcedureCatalogEntity>()
            .Property(p => p.BaselinePrice)
            .HasPrecision(18, 2);

        // Entity Relationships
        builder.Entity<AppointmentEntity>()
            .HasOne(a => a.Patient)
            .WithMany(p => p.Appointments)
            .HasForeignKey(a => a.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<DentalTreatmentEntity>()
            .HasOne(t => t.Patient)
            .WithMany(p => p.Treatments)
            .HasForeignKey(t => t.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<OrthodonticContractEntity>()
            .HasOne(c => c.Patient)
            .WithMany(p => p.OrthodonticContracts)
            .HasForeignKey(c => c.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<OrthodonticPaymentEntity>()
            .HasOne(p => p.Contract)
            .WithMany(c => c.Payments)
            .HasForeignKey(p => p.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for high-frequency queries
        builder.Entity<PatientEntity>()
            .HasIndex(p => new { p.LastName, p.FirstName });
        builder.Entity<PatientEntity>()
            .HasIndex(p => p.Phone);
        builder.Entity<PatientEntity>()
            .HasIndex(p => p.Status);
        builder.Entity<PatientEntity>()
            .HasIndex(p => p.CreatedAtUtc);

        builder.Entity<AppointmentEntity>()
            .HasIndex(a => a.PatientId);
        builder.Entity<AppointmentEntity>()
            .HasIndex(a => a.AppointmentDateTime);
        builder.Entity<AppointmentEntity>()
            .HasIndex(a => new { a.Status, a.AppointmentDateTime });

        builder.Entity<DentalTreatmentEntity>()
            .HasIndex(t => t.PatientId);
        builder.Entity<DentalTreatmentEntity>()
            .HasIndex(t => t.DateCreated);

        builder.Entity<OrthodonticContractEntity>()
            .HasIndex(c => c.PatientId);

        builder.Entity<OrthodonticPaymentEntity>()
            .HasIndex(p => p.ContractId);

        builder.Entity<ExpenseEntity>()
            .HasIndex(e => e.Date);
        builder.Entity<ExpenseEntity>()
            .HasIndex(e => new { e.Category, e.Date });

        builder.Entity<OutboxSyncRecordEntity>()
            .HasKey(o => o.Id);
        builder.Entity<OutboxSyncRecordEntity>()
            .HasIndex(o => new { o.EntityType, o.EntityId });

        builder.Entity<ClientLogEntryEntity>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Message).HasMaxLength(4000);
            b.Property(e => e.Exception).HasMaxLength(8000);
            b.Property(e => e.Category).HasMaxLength(200);
            b.Property(e => e.DeviceId).HasMaxLength(64);
            b.Property(e => e.CorrelationId).HasMaxLength(32);
            b.Property(e => e.Level).HasMaxLength(20);
            b.Property(e => e.DeviceModel).HasMaxLength(128);
            b.Property(e => e.Platform).HasMaxLength(64);
            b.Property(e => e.AppVersion).HasMaxLength(32);
            b.Property(e => e.UserEmail).HasMaxLength(256);

            b.HasIndex(e => e.ReceivedAtUtc);
            b.HasIndex(e => new { e.DeviceId, e.TimestampUtc });
            b.HasIndex(e => e.CorrelationId);
        });

        builder.Entity<PatientOutboxTaskEntity>(b =>
        {
            b.ToTable("PatientOutboxTasks");
            b.HasKey(t => t.Id);
            b.Property(t => t.Status).HasMaxLength(50);
            b.Property(t => t.PayloadJson).IsRequired();
            b.HasIndex(t => new { t.Status, t.CreatedAtUtc });
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries<BaseEntity>();
        var now = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
            }
            else if (entry.State == EntityState.Deleted)
            {
                // Convert hard-delete to soft-delete
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAtUtc = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}

