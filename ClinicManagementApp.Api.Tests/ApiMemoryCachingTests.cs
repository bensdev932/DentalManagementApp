using ClinicManagementApp.Api.Common;
using ClinicManagementApp.Api.Data;
using ClinicManagementApp.Api.Domain.Entities;
using ClinicManagementApp.Api.Models.DTOs.Expenses;
using ClinicManagementApp.Api.Models.DTOs.Treatments;
using ClinicManagementApp.Api.Services.Implementations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClinicManagementApp.Api.Tests;

[CollectionDefinition("FinancialCacheTests", DisableParallelization = true)]
public class FinancialCacheTestCollection
{
}

[Collection("FinancialCacheTests")]
public class ApiMemoryCachingTests
{
    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static IMemoryCache CreateMemoryCache()
    {
        return new MemoryCache(new MemoryCacheOptions());
    }

    [Fact]
    public async Task FinancialReportingService_DashboardSummary_ReturnsCachedResultOnSubsequentCalls()
    {
        // Arrange
        using var context = CreateDbContext();
        using var cache = CreateMemoryCache();

        var patient = new PatientEntity { FirstName = "Juan", LastName = "Dela Cruz", Phone = "09171234567" };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        var treatment = new DentalTreatmentEntity
        {
            PatientId = patient.Id,
            TreatmentType = "Oral Prophylaxis",
            ConsultationFee = 1500m,
            DateCreated = DateTime.UtcNow
        };
        context.DentalTreatments.Add(treatment);
        await context.SaveChangesAsync();

        var service = new FinancialReportingService(context, cache);

        // Act
        var firstResult = await service.GetDashboardSummaryAsync();

        // Mutate directly in DB without going through service (to test cache hit)
        context.DentalTreatments.Add(new DentalTreatmentEntity
        {
            PatientId = patient.Id,
            TreatmentType = "Extraction",
            ConsultationFee = 2000m,
            DateCreated = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var secondResult = await service.GetDashboardSummaryAsync();

        // Assert - secondResult should still match firstResult because of cache
        firstResult.Success.Should().BeTrue();
        secondResult.Success.Should().BeTrue();
        secondResult.Data!.TotalGrossIncome.Should().Be(1500m); // Cache hit!
    }

    [Fact]
    public async Task FinancialReportingService_PeriodSummary_ReturnsCachedResult()
    {
        // Arrange
        using var context = CreateDbContext();
        using var cache = CreateMemoryCache();

        var patient = new PatientEntity { FirstName = "Maria", LastName = "Santos", Phone = "09181234567" };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        var service = new FinancialReportingService(context, cache);

        // Act
        var result1 = await service.GetPeriodSummaryAsync("thismonth");
        result1.Success.Should().BeTrue();

        // Direct DB insert
        context.Expenses.Add(new ExpenseEntity
        {
            Title = "Supplies",
            Amount = 500m,
            Date = DateTime.UtcNow,
            Category = "Supplies"
        });
        await context.SaveChangesAsync();

        var result2 = await service.GetPeriodSummaryAsync("thismonth");

        // Assert: result2 is from cache, so total expenses should still be 0
        result2.Data!.TotalExpenses.Should().Be(0m);
    }

    [Fact]
    public async Task TreatmentService_CreateTreatmentAsync_InvalidatesFinancialSummaryCache()
    {
        // Arrange
        using var context = CreateDbContext();
        using var cache = CreateMemoryCache();
        var logger = NullLogger<TreatmentService>.Instance;

        var patient = new PatientEntity { FirstName = "Pedro", LastName = "Penduko", Phone = "09191234567" };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        var financialService = new FinancialReportingService(context, cache);
        var treatmentService = new TreatmentService(context, cache, logger);

        // Populate cache
        var initialSummary = await financialService.GetDashboardSummaryAsync();
        initialSummary.Data!.TotalGrossIncome.Should().Be(0m);

        // Act - Log treatment via TreatmentService (which triggers CacheKeys.InvalidateFinancialData)
        var createResult = await treatmentService.CreateTreatmentAsync(new CreateTreatmentRequest(
            patient.Id,
            "Root Canal",
            7000m,
            DateTime.UtcNow,
            false,
            null,
            null,
            null
        ));
        createResult.Success.Should().BeTrue();

        // Query financial summary again
        var updatedSummary = await financialService.GetDashboardSummaryAsync();

        // Assert - cache was evicted, fresh computation reflects new treatment!
        updatedSummary.Data!.TotalGrossIncome.Should().Be(7000m);
    }

    [Fact]
    public async Task TreatmentService_DeleteTreatmentAsync_InvalidatesFinancialSummaryCache()
    {
        // Arrange
        using var context = CreateDbContext();
        using var cache = CreateMemoryCache();
        var logger = NullLogger<TreatmentService>.Instance;

        var patient = new PatientEntity { FirstName = "Jose", LastName = "Rizal", Phone = "09201234567" };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        var treatment = new DentalTreatmentEntity
        {
            PatientId = patient.Id,
            TreatmentType = "Restoration",
            ConsultationFee = 1200m,
            DateCreated = DateTime.UtcNow
        };
        context.DentalTreatments.Add(treatment);
        await context.SaveChangesAsync();

        var financialService = new FinancialReportingService(context, cache);
        var treatmentService = new TreatmentService(context, cache, logger);

        // Populate cache
        var initialSummary = await financialService.GetDashboardSummaryAsync();
        initialSummary.Data!.TotalGrossIncome.Should().Be(1200m);

        // Act - Soft-delete treatment via TreatmentService
        var deleteResult = await treatmentService.DeleteTreatmentAsync(treatment.Id);
        deleteResult.Success.Should().BeTrue();

        // Query financial summary again
        var updatedSummary = await financialService.GetDashboardSummaryAsync();

        // Assert - cache was invalidated, soft-deleted treatment is excluded by query filter
        updatedSummary.Data!.TotalGrossIncome.Should().Be(0m);
    }

    [Fact]
    public async Task ExpenseService_Mutations_InvalidateFinancialSummaryCache()
    {
        // Arrange
        using var context = CreateDbContext();
        using var cache = CreateMemoryCache();
        var logger = NullLogger<ExpenseService>.Instance;

        var financialService = new FinancialReportingService(context, cache);
        var expenseService = new ExpenseService(context, cache, logger);

        // Populate cache with 0 expenses
        var initial = await financialService.GetDashboardSummaryAsync();
        initial.Data!.TotalExpenses.Should().Be(0m);

        // 1. Create Expense -> Invalidates cache
        var createResult = await expenseService.CreateExpenseAsync(new CreateExpenseRequest(
            "Dental Gloves & Masks",
            1500m,
            "Supplies",
            DateTime.UtcNow,
            false
        ));
        createResult.Success.Should().BeTrue();

        var afterCreate = await financialService.GetDashboardSummaryAsync();
        afterCreate.Data!.TotalExpenses.Should().Be(1500m);

        // 2. Update Expense -> Invalidates cache
        var updateResult = await expenseService.UpdateExpenseAsync(createResult.Data!.Id, new UpdateExpenseRequest(
            "Dental Gloves & Masks Bulk",
            2500m,
            "Supplies",
            DateTime.UtcNow,
            false
        ));
        updateResult.Success.Should().BeTrue();

        var afterUpdate = await financialService.GetDashboardSummaryAsync();
        afterUpdate.Data!.TotalExpenses.Should().Be(2500m);

        // 3. Delete Expense -> Invalidates cache
        var deleteResult = await expenseService.DeleteExpenseAsync(createResult.Data!.Id);
        deleteResult.Success.Should().BeTrue();

        var afterDelete = await financialService.GetDashboardSummaryAsync();
        afterDelete.Data!.TotalExpenses.Should().Be(0m);
    }

    [Fact]
    public async Task TreatmentService_ProcedureCatalog_CachesAndInvalidatesOnAdd()
    {
        // Arrange
        using var context = CreateDbContext();
        using var cache = CreateMemoryCache();
        var logger = NullLogger<TreatmentService>.Instance;

        var service = new TreatmentService(context, cache, logger);

        // Act 1: GetProcedureCatalogAsync seeds default catalog and caches it
        var catalog1 = await service.GetProcedureCatalogAsync();
        catalog1.Success.Should().BeTrue();
        catalog1.Data!.Count.Should().BeGreaterThan(0);
        var initialCount = catalog1.Data!.Count;

        // Verify it is cached
        cache.TryGetValue(CacheKeys.ProcedureCatalog, out _).Should().BeTrue();

        // Act 2: AddProcedureToCatalogAsync evicts catalog cache
        var addResult = await service.AddProcedureToCatalogAsync(new CreateProcedureRequest(
            "Laser Teeth Whitening",
            "Cosmetic",
            12000m,
            "Advanced diode laser bleaching"
        ));
        addResult.Success.Should().BeTrue();

        // Assert: Cache key should be evicted
        cache.TryGetValue(CacheKeys.ProcedureCatalog, out _).Should().BeFalse();

        // Act 3: Next fetch reads new procedure and re-populates cache
        var catalog2 = await service.GetProcedureCatalogAsync();
        catalog2.Data!.Count.Should().Be(initialCount + 1);
        catalog2.Data.Should().Contain(p => p.Name == "Laser Teeth Whitening");
    }

    [Fact]
    public async Task TaxCalculationService_DeadlinesAndQuarterlyTax_AreCachedAndInvalidated()
    {
        // Arrange
        using var context = CreateDbContext();
        using var cache = CreateMemoryCache();

        var patient = new PatientEntity { FirstName = "Andres", LastName = "Bonifacio", Phone = "09211234567" };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        context.DentalTreatments.Add(new DentalTreatmentEntity
        {
            PatientId = patient.Id,
            TreatmentType = "Braces Downpayment",
            ConsultationFee = 300000m, // Above 250k exemption
            DateCreated = now
        });
        await context.SaveChangesAsync();

        var taxService = new TaxCalculationService(context, cache);

        // 1. Deadlines caching
        var deadlines1 = await taxService.GetUpcomingDeadlinesAsync();
        deadlines1.Success.Should().BeTrue();
        cache.TryGetValue(CacheKeys.TaxDeadlines, out _).Should().BeTrue();

        // 2. Quarterly tax calculation caching
        var quarter = (now.Month - 1) / 3 + 1;
        var tax1 = await taxService.CalculateQuarterlyTaxAsync(now.Year, quarter, "EightPercentFlat", "OSD", 0m);
        tax1.Success.Should().BeTrue();
        tax1.Data!.IncomeTaxDue.Should().Be((300000m - 250000m) * 0.08m);

        var taxKey = CacheKeys.QuarterlyTax(now.Year, quarter, "EightPercentFlat", "OSD", 0m);
        cache.TryGetValue(taxKey, out _).Should().BeTrue();

        // 3. Invalidate via financial change token
        CacheKeys.InvalidateFinancialData(cache);

        // Assert: Quarterly tax entry is evicted via change token
        cache.TryGetValue(taxKey, out _).Should().BeFalse();
    }
}

