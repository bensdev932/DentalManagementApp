using System.Text.Json;
using ClinicManagementApp.Api.Common;
using ClinicManagementApp.Api.Data;
using ClinicManagementApp.Api.Domain.Entities;
using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Patients;
using ClinicManagementApp.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ClinicManagementApp.Api.Services.Implementations;

/// <summary>
/// Implements patient lifecycle management, outbox enqueuing, and cached clinical data retrieval.
/// </summary>
public class PatientService(
    ApplicationDbContext context,
    IMemoryCache memoryCache,
    IPatientOutboxQueueNotifier queueNotifier,
    ILogger<PatientService> logger) : IPatientService
{
    public async Task<ApiResponse<PagedResult<PatientDto>>> GetPatientsAsync(PatientFilterParams filter)
    {
        var query = context.Patients.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(p =>
                p.FirstName.ToLower().Contains(search) ||
                p.LastName.ToLower().Contains(search) ||
                p.Phone.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(p => p.Status.ToLower() == filter.Status.Trim().ToLower());
        }

        var totalCount = await query.CountAsync();

        var patients = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(p => MapToDto(p))
            .ToListAsync();

        var pagedResult = new PagedResult<PatientDto>(patients, totalCount, filter.Page, filter.PageSize);
        return ApiResponse<PagedResult<PatientDto>>.SuccessResult(pagedResult);
    }

    public async Task<ApiResponse<PatientDetailDto>> GetPatientByIdAsync(int id)
    {
        var cacheKey = CacheKeys.PatientById(id);
        if (memoryCache.TryGetValue(cacheKey, out PatientDetailDto? cachedPatient) && cachedPatient != null)
        {
            return ApiResponse<PatientDetailDto>.SuccessResult(cachedPatient);
        }

        var patient = await context.Patients
            .AsNoTracking()
            .Include(p => p.Appointments)
            .Include(p => p.Treatments)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (patient == null)
        {
            return ApiResponse<PatientDetailDto>.FailureResult($"Patient with ID {id} was not found.");
        }

        var detailDto = new PatientDetailDto(
            patient.Id,
            patient.FirstName,
            patient.LastName,
            $"{patient.FirstName} {patient.LastName}".Trim(),
            patient.Age,
            patient.Gender,
            patient.Phone,
            patient.Role,
            patient.ConsultationFee,
            patient.ConsultationNotes,
            patient.Status,
            patient.CreatedAtUtc,
            patient.UpdatedAtUtc,
            patient.Appointments.Count,
            patient.Treatments.Count
        );

        memoryCache.Set(cacheKey, detailDto, TimeSpan.FromMinutes(30));

        return ApiResponse<PatientDetailDto>.SuccessResult(detailDto);
    }

    public async Task<ApiResponse<PatientDto>> EnqueuePatientCreationAsync(CreatePatientRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
        {
            return ApiResponse<PatientDto>.FailureResult("First name and last name are required.");
        }

        var outboxTask = new PatientOutboxTaskEntity
        {
            Id = Guid.NewGuid(),
            PayloadJson = JsonSerializer.Serialize(request),
            Status = "Pending",
            RetryCount = 0,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.PatientOutboxTasks.Add(outboxTask);
        await context.SaveChangesAsync();

        queueNotifier.Notify();

        logger.LogInformation("Patient creation task enqueued: {FirstName} {LastName} (TaskId: {TaskId})",
            request.FirstName, request.LastName, outboxTask.Id);

        var pendingDto = new PatientDto(
            0,
            request.FirstName.Trim(),
            request.LastName.Trim(),
            $"{request.FirstName.Trim()} {request.LastName.Trim()}".Trim(),
            request.Age,
            request.Gender?.Trim() ?? string.Empty,
            request.Phone?.Trim() ?? string.Empty,
            string.IsNullOrWhiteSpace(request.Role) ? "Oral Prophylaxis (Cleaning)" : request.Role.Trim(),
            request.ConsultationFee,
            request.ConsultationNotes?.Trim() ?? string.Empty,
            "Pending",
            outboxTask.CreatedAtUtc,
            null
        );

        return ApiResponse<PatientDto>.SuccessResult(pendingDto, "Patient creation task enqueued successfully.");
    }

    public async Task<ApiResponse<PatientDto>> CreatePatientAsync(CreatePatientRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
        {
            return ApiResponse<PatientDto>.FailureResult("First name and last name are required.");
        }

        var patient = new PatientEntity
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Age = request.Age,
            Gender = request.Gender?.Trim() ?? string.Empty,
            Phone = request.Phone?.Trim() ?? string.Empty,
            Role = string.IsNullOrWhiteSpace(request.Role) ? "Oral Prophylaxis (Cleaning)" : request.Role.Trim(),
            ConsultationFee = request.ConsultationFee,
            ConsultationNotes = request.ConsultationNotes?.Trim() ?? string.Empty,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Active" : request.Status.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        var dto = MapToDto(patient);
        var detailDto = new PatientDetailDto(
            patient.Id,
            patient.FirstName,
            patient.LastName,
            dto.FullName,
            patient.Age,
            patient.Gender,
            patient.Phone,
            patient.Role,
            patient.ConsultationFee,
            patient.ConsultationNotes,
            patient.Status,
            patient.CreatedAtUtc,
            patient.UpdatedAtUtc,
            0,
            0
        );

        memoryCache.Set(CacheKeys.PatientById(patient.Id), detailDto, TimeSpan.FromMinutes(30));
        memoryCache.Set(CacheKeys.PatientDto(patient.Id), dto, TimeSpan.FromMinutes(30));

        if (patient.ConsultationFee > 0)
        {
            CacheKeys.InvalidateFinancialData(memoryCache);
        }

        logger.LogInformation("Patient registered: {FullName} (ID: {Id})", $"{patient.FirstName} {patient.LastName}", patient.Id);
        return ApiResponse<PatientDto>.SuccessResult(dto, "Patient registered successfully.");
    }

    public async Task<ApiResponse<PatientDto>> UpdatePatientAsync(int id, UpdatePatientRequest request)
    {
        var patient = await context.Patients.FindAsync(id);
        if (patient == null)
        {
            return ApiResponse<PatientDto>.FailureResult($"Patient with ID {id} was not found.");
        }

        patient.FirstName = request.FirstName.Trim();
        patient.LastName = request.LastName.Trim();
        patient.Age = request.Age;
        patient.Gender = request.Gender?.Trim() ?? string.Empty;
        patient.Phone = request.Phone?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(request.Role)) patient.Role = request.Role.Trim();
        patient.ConsultationFee = request.ConsultationFee;
        if (request.ConsultationNotes != null) patient.ConsultationNotes = request.ConsultationNotes.Trim();
        if (!string.IsNullOrWhiteSpace(request.Status)) patient.Status = request.Status.Trim();
        patient.UpdatedAtUtc = DateTime.UtcNow;

        await context.SaveChangesAsync();

        memoryCache.Remove(CacheKeys.PatientById(id));
        memoryCache.Remove(CacheKeys.PatientDto(id));

        logger.LogInformation("Patient updated: ID {Id}", patient.Id);

        return ApiResponse<PatientDto>.SuccessResult(MapToDto(patient), "Patient updated successfully.");
    }

    public async Task<ApiResponse> DeletePatientAsync(int id)
    {
        var patient = await context.Patients
            .Include(p => p.Treatments)
            .Include(p => p.Appointments)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (patient == null)
        {
            return ApiResponse.FailureResult($"Patient with ID {id} was not found.");
        }

        var now = DateTime.UtcNow;

        // Cascade soft-delete across linked treatments and appointments
        patient.IsDeleted = true;
        patient.DeletedAtUtc = now;

        foreach (var treatment in patient.Treatments)
        {
            treatment.IsDeleted = true;
            treatment.DeletedAtUtc = now;
        }

        foreach (var appointment in patient.Appointments)
        {
            appointment.IsDeleted = true;
            appointment.DeletedAtUtc = now;
        }

        await context.SaveChangesAsync();

        memoryCache.Remove(CacheKeys.PatientById(id));
        memoryCache.Remove(CacheKeys.PatientDto(id));

        logger.LogInformation("Patient and associated clinical records archived (ID: {Id})", id);

        return ApiResponse.SuccessResult("Patient and linked clinical records archived successfully.");
    }

    private static PatientDto MapToDto(PatientEntity entity) =>
        new(
            entity.Id,
            entity.FirstName,
            entity.LastName,
            $"{entity.FirstName} {entity.LastName}".Trim(),
            entity.Age,
            entity.Gender,
            entity.Phone,
            entity.Role,
            entity.ConsultationFee,
            entity.ConsultationNotes,
            entity.Status,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc
        );
}
