using ClinicManagementApp.Api.Data;
using ClinicManagementApp.Api.Domain.Entities;
using ClinicManagementApp.Api.Domain.ValueObjects;
using ClinicManagementApp.Api.Models.DTOs.Orthodontics;
using ClinicManagementApp.Api.Services.Implementations;
using ClinicManagementApp.Api.Services.Implementations.Orthodontics;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClinicManagementApp.Api.Tests;

public class OrthodonticsTests
{
    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetPatientSummaryAsync_WhenNoContractExists_ReturnsZeroedSummaryWithHasContractFalse()
    {
        // Arrange
        using var context = CreateDbContext();
        var service = new OrthodonticsService(context, NullLogger<OrthodonticsService>.Instance);

        // Act
        var response = await service.GetPatientSummaryAsync(patientId: 999);

        // Assert
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.HasContract.Should().BeFalse();
        response.Data.IsActive.Should().BeFalse();
        response.Data.TotalContractPrice.Should().Be(0m);
        response.Data.DownPayment.Should().Be(0m);
        response.Data.RemainingBalance.Should().Be(0m);
    }

    [Fact]
    public async Task GetPatientSummaryAsync_WithActiveContractAndPayments_CalculatesAmortizationAccurately()
    {
        // Arrange
        using var context = CreateDbContext();
        var patient = new PatientEntity { FirstName = "Juan", LastName = "Luna", Phone = "09171112233" };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        var contract = new OrthodonticContractEntity
        {
            PatientId = patient.Id,
            TotalContractPrice = 60000m,
            DownPayment = 10000m,
            TermsMonths = 20,
            IsActive = true
        };
        context.OrthodonticContracts.Add(contract);
        await context.SaveChangesAsync();

        context.OrthodonticPayments.AddRange(
            new OrthodonticPaymentEntity { ContractId = contract.Id, PatientId = patient.Id, Amount = 2500m },
            new OrthodonticPaymentEntity { ContractId = contract.Id, PatientId = patient.Id, Amount = 2500m }
        );
        await context.SaveChangesAsync();

        var service = new OrthodonticsService(context, NullLogger<OrthodonticsService>.Instance);

        // Act
        var response = await service.GetPatientSummaryAsync(patient.Id);

        // Assert
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        var summary = response.Data!;
        summary.HasContract.Should().BeTrue();
        summary.IsActive.Should().BeTrue();
        summary.TotalContractPrice.Should().Be(60000m);
        summary.DownPayment.Should().Be(10000m);
        summary.TotalPaid.Should().Be(15000m); // 10000 + 2500 + 2500
        summary.RemainingBalance.Should().Be(45000m); // 60000 - 15000
        summary.MonthlyAmortization.Should().Be(2500m); // (60000 - 10000) / 20
        summary.TermsRemaining.Should().Be(18); // 45000 / 2500
        summary.PaymentProgress.Should().Be(0.25); // 15000 / 60000 = 0.25
        summary.IsFullyPaid.Should().BeFalse();
    }

    [Fact]
    public async Task CreateContractAsync_WhenPatientNotFound_ReturnsFailure()
    {
        // Arrange
        using var context = CreateDbContext();
        var service = new OrthodonticsService(context, NullLogger<OrthodonticsService>.Instance);
        var request = new CreateContractRequest(999, 50000m, 5000m, 24, null);

        // Act
        var response = await service.CreateContractAsync(request);

        // Assert
        response.Success.Should().BeFalse();
        response.Message.Should().Contain("was not found");
    }

    [Fact]
    public async Task CreateContractAsync_WhenActiveContractExists_DeactivatesPreviousContractAndCreatesNew()
    {
        // Arrange
        using var context = CreateDbContext();
        var patient = new PatientEntity { FirstName = "Jose", LastName = "Rizal", Phone = "09181112233" };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        var oldContract = new OrthodonticContractEntity
        {
            PatientId = patient.Id,
            TotalContractPrice = 40000m,
            DownPayment = 5000m,
            TermsMonths = 12,
            IsActive = true
        };
        context.OrthodonticContracts.Add(oldContract);
        await context.SaveChangesAsync();

        var service = new OrthodonticsService(context, NullLogger<OrthodonticsService>.Instance);
        var request = new CreateContractRequest(patient.Id, 70000m, 10000m, 24, null);

        // Act
        var response = await service.CreateContractAsync(request);

        // Assert
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.TotalContractPrice.Should().Be(70000m);

        var refreshedOld = await context.OrthodonticContracts.FindAsync(oldContract.Id);
        refreshedOld!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task CreateContractAsync_WithDownPayment_PostsTreatmentRecordToLedger()
    {
        // Arrange
        using var context = CreateDbContext();
        var patient = new PatientEntity { FirstName = "Apolinario", LastName = "Mabini", Phone = "09191112233" };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        var service = new OrthodonticsService(context, NullLogger<OrthodonticsService>.Instance);
        var request = new CreateContractRequest(patient.Id, 50000m, 8000m, 24, null);

        // Act
        var response = await service.CreateContractAsync(request);

        // Assert
        response.Success.Should().BeTrue();
        var treatments = await context.DentalTreatments.Where(t => t.PatientId == patient.Id).ToListAsync();
        treatments.Should().HaveCount(1);
        treatments[0].TreatmentType.Should().Be("Braces (Down Payment)");
        treatments[0].ConsultationFee.Should().Be(8000m);
        treatments[0].IsInstallment.Should().BeTrue();
    }

    [Fact]
    public async Task RecordPaymentAsync_WhenNoActiveContract_ReturnsFailure()
    {
        // Arrange
        using var context = CreateDbContext();
        var patient = new PatientEntity { FirstName = "Emilio", LastName = "Aguinaldo", Phone = "09201112233" };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        var service = new OrthodonticsService(context, NullLogger<OrthodonticsService>.Instance);
        var request = new RecordPaymentRequest(patient.Id, 2500m, null, "Monthly payment");

        // Act
        var response = await service.RecordPaymentAsync(request);

        // Assert
        response.Success.Should().BeFalse();
        response.Message.Should().Contain("No active orthodontic contract found");
    }

    [Fact]
    public async Task RecordPaymentAsync_WithActiveContract_RecordsPaymentAndAddsTreatmentLedger()
    {
        // Arrange
        using var context = CreateDbContext();
        var patient = new PatientEntity { FirstName = "Melchora", LastName = "Aquino", Phone = "09211112233" };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        var contract = new OrthodonticContractEntity
        {
            PatientId = patient.Id,
            TotalContractPrice = 50000m,
            DownPayment = 5000m,
            TermsMonths = 24,
            IsActive = true
        };
        context.OrthodonticContracts.Add(contract);
        await context.SaveChangesAsync();

        var service = new OrthodonticsService(context, NullLogger<OrthodonticsService>.Instance);
        var request = new RecordPaymentRequest(patient.Id, 2000m, null, "Month 1 adjustment");

        // Act
        var response = await service.RecordPaymentAsync(request);

        // Assert
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Amount.Should().Be(2000m);

        var payments = await context.OrthodonticPayments.Where(p => p.PatientId == patient.Id).ToListAsync();
        payments.Should().HaveCount(1);
        payments[0].Amount.Should().Be(2000m);

        var treatments = await context.DentalTreatments.Where(t => t.PatientId == patient.Id).ToListAsync();
        treatments.Should().HaveCount(1);
        treatments[0].TreatmentType.Should().Be("Braces Adjustment");
        treatments[0].ConsultationFee.Should().Be(2000m);
    }

    [Fact]
    public async Task GetPatientPaymentsAsync_ReturnsPaymentsOrderedByDate()
    {
        // Arrange
        using var context = CreateDbContext();
        var patient = new PatientEntity { FirstName = "Gabriela", LastName = "Silang", Phone = "09221112233" };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        var contract = new OrthodonticContractEntity
        {
            PatientId = patient.Id,
            TotalContractPrice = 50000m,
            DownPayment = 5000m,
            TermsMonths = 24,
            IsActive = true
        };
        context.OrthodonticContracts.Add(contract);
        await context.SaveChangesAsync();

        var date1 = DateTime.UtcNow.AddDays(-20);
        var date2 = DateTime.UtcNow.AddDays(-10);

        context.OrthodonticPayments.AddRange(
            new OrthodonticPaymentEntity { ContractId = contract.Id, PatientId = patient.Id, Amount = 1500m, PaymentDate = date1 },
            new OrthodonticPaymentEntity { ContractId = contract.Id, PatientId = patient.Id, Amount = 2000m, PaymentDate = date2 }
        );
        await context.SaveChangesAsync();

        var service = new OrthodonticsService(context, NullLogger<OrthodonticsService>.Instance);

        // Act
        var response = await service.GetPatientPaymentsAsync(patient.Id);

        // Assert
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Should().HaveCount(2);
        response.Data![0].Amount.Should().Be(2000m); // Ordered descending by date
        response.Data[1].Amount.Should().Be(1500m);
    }

    [Fact]
    public void Money_ValueObject_EnforcesNonNegativeAndArithmeticInvariants()
    {
        // Assert invariants
        var action = () => new Money(-100m);
        action.Should().Throw<ArgumentOutOfRangeException>();

        var m1 = new Money(5000m);
        var m2 = new Money(2000m);

        (m1 + m2).Amount.Should().Be(7000m);
        (m1 - m2).Amount.Should().Be(3000m);
        (m2 - m1).Amount.Should().Be(0m); // Floored to zero
        (m1 / 2).Amount.Should().Be(2500m);
        m1.ToString().Should().Contain("5,000.00");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void DomainIdentifiers_EnforcePositiveValueInvariant(int invalidId)
    {
        var createPatientId = () => new PatientId(invalidId);
        var createContractId = () => new ContractId(invalidId);

        createPatientId.Should().Throw<ArgumentException>();
        createContractId.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task GetOrthodonticSummaryHandler_IsolatedExecution_ComputesAccurateMetrics()
    {
        // Arrange
        using var context = CreateDbContext();
        var patient = new PatientEntity { FirstName = "Teresa", LastName = "Magbanua", Phone = "09231112233" };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        var contract = new OrthodonticContractEntity
        {
            PatientId = patient.Id,
            TotalContractPrice = 48000m,
            DownPayment = 8000m,
            TermsMonths = 20,
            IsActive = true
        };
        context.OrthodonticContracts.Add(contract);
        await context.SaveChangesAsync();

        var handler = new GetOrthodonticSummaryHandler(context);

        // Act
        var result = await handler.HandleAsync(new PatientId(patient.Id));

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.RemainingBalance.Should().Be(40000m);
        result.Data.MonthlyAmortization.Should().Be(2000m); // (48000 - 8000) / 20
        result.Data.TermsRemaining.Should().Be(20);
    }

    [Fact]
    public async Task CreateOrthodonticContractHandler_IsolatedExecution_CreatesContractSuccessfully()
    {
        // Arrange
        using var context = CreateDbContext();
        var patient = new PatientEntity { FirstName = "Marcelo", LastName = "Del Pilar", Phone = "09241112233" };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        var handler = new CreateOrthodonticContractHandler(context, NullLogger<CreateOrthodonticContractHandler>.Instance);
        var request = new CreateContractRequest(patient.Id, 55000m, 10000m, 24, null);

        // Act
        var result = await handler.HandleAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.TotalContractPrice.Should().Be(55000m);
        result.Data.DownPayment.Should().Be(10000m);
    }

    [Fact]
    public async Task RecordOrthodonticPaymentHandler_IsolatedExecution_RecordsPaymentSuccessfully()
    {
        // Arrange
        using var context = CreateDbContext();
        var patient = new PatientEntity { FirstName = "Antonio", LastName = "Luna", Phone = "09251112233" };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        var contract = new OrthodonticContractEntity
        {
            PatientId = patient.Id,
            TotalContractPrice = 50000m,
            DownPayment = 5000m,
            TermsMonths = 24,
            IsActive = true
        };
        context.OrthodonticContracts.Add(contract);
        await context.SaveChangesAsync();

        var handler = new RecordOrthodonticPaymentHandler(context, NullLogger<RecordOrthodonticPaymentHandler>.Instance);
        var request = new RecordPaymentRequest(patient.Id, 2500m, null, "Month 1 payment");

        // Act
        var result = await handler.HandleAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.Amount.Should().Be(2500m);
    }

    [Fact]
    public async Task GetOrthodonticPaymentsHandler_IsolatedExecution_ReturnsPayments()
    {
        // Arrange
        using var context = CreateDbContext();
        var patient = new PatientEntity { FirstName = "Diego", LastName = "Silang", Phone = "09261112233" };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        var contract = new OrthodonticContractEntity
        {
            PatientId = patient.Id,
            TotalContractPrice = 50000m,
            DownPayment = 5000m,
            TermsMonths = 24,
            IsActive = true
        };
        context.OrthodonticContracts.Add(contract);
        await context.SaveChangesAsync();

        context.OrthodonticPayments.Add(new OrthodonticPaymentEntity
        {
            ContractId = contract.Id,
            PatientId = patient.Id,
            Amount = 3000m,
            PaymentDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var handler = new GetOrthodonticPaymentsHandler(context);

        // Act
        var result = await handler.HandleAsync(new PatientId(patient.Id));

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.Should().HaveCount(1);
        result.Data![0].Amount.Should().Be(3000m);
    }
}
