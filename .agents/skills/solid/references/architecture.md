# Software Architecture in C# & .NET

## The Goal of Architecture

Enable the development team to:
1. **Add** features with minimal friction
2. **Change** existing features safely
3. **Remove** features cleanly
4. **Test** features in isolation
5. **Deploy** independently when possible

## Architectural Principles

### 1. Vertical Boundaries (Vertical Slice Architecture)

Organize by **feature / slice**, not by technical layer.

```
BAD: Layer-first
ClinicManagementApp.Api/
  Controllers/
    PatientsController.cs
    AppointmentsController.cs
  Services/
    PatientService.cs
    AppointmentService.cs
  Repositories/
    PatientRepository.cs
    AppointmentRepository.cs

GOOD: Feature / Vertical Slice
ClinicManagementApp.Api/
  Features/
    Patients/
      CreatePatient/
        CreatePatientCommand.cs
        CreatePatientHandler.cs
        CreatePatientEndpoint.cs
      GetPatient/
        GetPatientQuery.cs
        GetPatientHandler.cs
    Appointments/
      BookAppointment/
        BookAppointmentCommand.cs
        BookAppointmentHandler.cs
```

**Why:** Changes to "Patients" stay in `Patients/`. High cohesion within features, minimal ripple effects across the application.

### 2. Horizontal Boundaries (Layers)

Separate concerns with clear dependency rules:

```
┌──────────────────────────────────────┐
│           Presentation               │  Minimal APIs, Controllers, Filters, Blazor/MAUI
├──────────────────────────────────────┤
│           Application                │  Commands, Queries, Handlers, DTOs
├──────────────────────────────────────┤
│             Domain                   │  Entities, Value Objects, Domain Exceptions
├──────────────────────────────────────┤
│          Infrastructure              │  EF Core DbContext, Migrations, External APIs
└──────────────────────────────────────┘
```

### 3. The Dependency Rule

**Dependencies point INWARD.**

```
Infrastructure → Application → Domain
      ↓               ↓            ↓
   (outer)        (middle)      (inner)
```

- Inner layers know NOTHING about outer layers.
- Domain has zero dependencies on EF Core, ASP.NET Core, or third-party SDKs.
- Use interfaces and abstractions registered in `IServiceCollection`.

```csharp
// Domain defines the abstraction (inner)
public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(Money amount, CancellationToken ct = default);
}

// Infrastructure implements it (outer)
public class StripePaymentGateway(HttpClient httpClient) : IPaymentGateway
{
    public async Task<PaymentResult> ChargeAsync(Money amount, CancellationToken ct = default)
    {
        // Stripe HTTP API calls
    }
}

// Application Handler uses the interface
public class CheckoutHandler(IPaymentGateway paymentGateway)
{
    public async Task HandleAsync(CheckoutCommand cmd, CancellationToken ct)
    {
        await paymentGateway.ChargeAsync(cmd.Total, ct);
    }
}
```

### 4. Cross-Cutting Concerns

Concerns that span multiple features: logging, auth, validation, exception handling.

**Idiomatic .NET Approaches:**
- **ASP.NET Core Middleware** (Global Exception Handling, Request Logging)
- **Action / Endpoint Filters** (`EndpointFilterDelegate`)
- **MediatR / Pipeline Behaviors** (`IPipelineBehavior<TRequest, TResponse>`)

```csharp
// Global Exception Handling Middleware
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception occurred: {Message}", ex.Message);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(ApiResponse<string>.FailureResult("An unexpected error occurred."));
        }
    }
}
```

---

## Architectural Styles in .NET

### Vertical Slice Architecture
Each request is treated as a distinct slice through the architecture. Reduces ceremony, avoids god services.

### Clean Architecture / Onion Architecture
Domain at center, surrounded by Application, then Infrastructure and Presentation. Ideal for complex enterprise business domains.

---

## Red Flags in Architecture

- **Circular dependencies** between projects or namespaces
- **Domain depending on EF Core or ASP.NET Core**
- **Fat generic repositories** wrapping DbContext without added value
- **Fat god services** (`PatientService` with 30 disparate methods)
- **Shared mutable state** across async tasks
- **Database schema driving domain models** instead of domain rules driving persistence
