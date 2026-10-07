using ClinicManagementApp.Api.Common;
using ClinicManagementApp.Api.Data;
using ClinicManagementApp.Api.Domain.Entities;
using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Treatments;
using ClinicManagementApp.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ClinicManagementApp.Api.Services.Implementations;

/// <summary>
/// Implements clinical treatment logging, ledger calculation records, and standard procedure pricing catalogs with memory caching.
/// </summary>
public class TreatmentService(
    ApplicationDbContext context,
    IMemoryCache memoryCache,
    ILogger<TreatmentService> logger) : ITreatmentService
{
    public async Task<ApiResponse<IReadOnlyList<DentalTreatmentDto>>> GetTreatmentsAsync(TreatmentFilterParams filter)
    {
        var query = context.DentalTreatments.AsNoTracking().AsQueryable();

        if (filter.StartDate.HasValue)
        {
            query = query.Where(t => t.DateCreated >= filter.StartDate.Value);
        }

        if (filter.EndDate.HasValue)
        {
            query = query.Where(t => t.DateCreated <= filter.EndDate.Value);
        }

        var treatments = await query
            .OrderByDescending(t => t.DateCreated)
            .Select(t => MapToDto(t))
            .ToListAsync();

        return ApiResponse<IReadOnlyList<DentalTreatmentDto>>.SuccessResult(treatments);
    }

    public async Task<ApiResponse<IReadOnlyList<DentalTreatmentDto>>> GetPatientTreatmentsAsync(int patientId)
    {
        var treatments = await context.DentalTreatments
            .AsNoTracking()
            .Where(t => t.PatientId == patientId)
            .OrderByDescending(t => t.DateCreated)
            .Select(t => MapToDto(t))
            .ToListAsync();

        return ApiResponse<IReadOnlyList<DentalTreatmentDto>>.SuccessResult(treatments);
    }

    public async Task<ApiResponse<DentalTreatmentDto>> CreateTreatmentAsync(CreateTreatmentRequest request)
    {
        var patient = await context.Patients.FindAsync(request.PatientId);
        if (patient == null)
        {
            return ApiResponse<DentalTreatmentDto>.FailureResult($"Patient with ID {request.PatientId} was not found.");
        }

        var treatment = new DentalTreatmentEntity
        {
            PatientId = request.PatientId,
            TreatmentType = request.TreatmentType.Trim(),
            ConsultationFee = request.ConsultationFee,
            DateCreated = request.DateCreated.HasValue ? DateTime.SpecifyKind(request.DateCreated.Value, DateTimeKind.Utc) : DateTime.UtcNow,
            IsInstallment = request.IsInstallment,
            TotalContractPrice = request.TotalContractPrice ?? 0,
            DownPayment = request.DownPayment ?? 0,
            TermsMonths = request.TermsMonths ?? 0,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.DentalTreatments.Add(treatment);
        await context.SaveChangesAsync();

        CacheKeys.InvalidateFinancialData(memoryCache);

        logger.LogInformation("Treatment registered: #{Id} for Patient #{PatientId}, Procedure: {Type}",
            treatment.Id, treatment.PatientId, treatment.TreatmentType);

        return ApiResponse<DentalTreatmentDto>.SuccessResult(MapToDto(treatment), "Treatment recorded successfully.");
    }

    public async Task<ApiResponse> DeleteTreatmentAsync(int id)
    {
        var treatment = await context.DentalTreatments.FindAsync(id);
        if (treatment == null)
        {
            return ApiResponse.FailureResult($"Treatment with ID {id} was not found.");
        }

        treatment.IsDeleted = true;
        treatment.DeletedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync();

        CacheKeys.InvalidateFinancialData(memoryCache);

        logger.LogInformation("Treatment #{Id} soft-deleted from ledger", id);
        return ApiResponse.SuccessResult("Treatment entry archived successfully.");
    }

    public async Task<ApiResponse<IReadOnlyList<ProcedureCatalogDto>>> GetProcedureCatalogAsync()
    {
        var procedures = await memoryCache.GetOrCreateAsync(
            CacheKeys.ProcedureCatalog,
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);

                var list = await context.Procedures
                    .AsNoTracking()
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.Category)
                    .ThenBy(p => p.Name)
                    .Select(p => MapToCatalogDto(p))
                    .ToListAsync();

                if (list.Count == 0)
                {
                    // Seed baseline catalog matching mobile ProcedureCatalog
                    var seeded = SeedDefaultCatalog();
                    context.Procedures.AddRange(seeded);
                    await context.SaveChangesAsync();

                    list = seeded.Select(MapToCatalogDto).ToList();
                }

                return list;
            });

        return ApiResponse<IReadOnlyList<ProcedureCatalogDto>>.SuccessResult(procedures!);
    }

    public async Task<ApiResponse<ProcedureCatalogDto>> AddProcedureToCatalogAsync(CreateProcedureRequest request)
    {
        var procedure = new ProcedureCatalogEntity
        {
            Name = request.Name.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim(),
            BaselinePrice = request.BaselinePrice,
            Description = request.Description?.Trim() ?? string.Empty,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.Procedures.Add(procedure);
        await context.SaveChangesAsync();

        memoryCache.Remove(CacheKeys.ProcedureCatalog);

        return ApiResponse<ProcedureCatalogDto>.SuccessResult(MapToCatalogDto(procedure), "Procedure added to catalog.");
    }

    private static List<ProcedureCatalogEntity> SeedDefaultCatalog() =>
    [
        new() { Name = "Oral Prophylaxis (Cleaning)", Category = "Preventive", BaselinePrice = 800.00m, Description = "Ultrasonic plaque scaling & polishing" },
        new() { Name = "Tooth Extraction", Category = "Surgical", BaselinePrice = 1000.00m, Description = "Simple or surgical tooth removal" },
        new() { Name = "Dental Fillings (Restoration)", Category = "Restorative", BaselinePrice = 1200.00m, Description = "Composite tooth-colored resin filling" },
        new() { Name = "Teeth Whitening", Category = "Cosmetic", BaselinePrice = 5000.00m, Description = "In-office light-activated bleaching" },
        new() { Name = "Root Canal Therapy", Category = "Endodontics", BaselinePrice = 7000.00m, Description = "Pulp extirpation & canal obturation" },
        new() { Name = "Dental Crowns & Bridges", Category = "Prosthodontics", BaselinePrice = 8000.00m, Description = "Porcelain/Zirconia fixed crowns" },
        new() { Name = "Braces", Category = "Orthodontics", BaselinePrice = 45000.00m, Description = "Full metal orthodontic brackets" },
        new() { Name = "Braces Adjustment", Category = "Orthodontics", BaselinePrice = 1000.00m, Description = "Monthly bracket & archwire adjustment" },
        new() { Name = "Aligners", Category = "Orthodontics", BaselinePrice = 60000.00m, Description = "Clear custom sequential aligners" }
    ];

    private static DentalTreatmentDto MapToDto(DentalTreatmentEntity t) =>
        new(
            t.Id,
            t.PatientId,
            t.TreatmentType,
            t.ConsultationFee,
            t.DateCreated,
            t.IsInstallment,
            t.TotalContractPrice,
            t.DownPayment,
            t.TermsMonths,
            t.CreatedAtUtc
        );

    private static ProcedureCatalogDto MapToCatalogDto(ProcedureCatalogEntity p) =>
        new(
            p.Id,
            p.Name,
            p.Category,
            p.BaselinePrice,
            p.Description,
            p.IsActive
        );
}

