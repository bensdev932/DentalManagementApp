# Design Patterns in C# & .NET

## What Are Design Patterns?

Reusable solutions to common design problems. A shared vocabulary for discussing architecture and component collaboration in C#.

## WARNING: Don't Force Patterns

> "Let patterns emerge from refactoring, don't force them upfront."

Patterns should solve real problems you HAVE, not hypothetical problems you MIGHT have.

---

## Creational Patterns

### Factory / Factory Method
**Purpose:** Create objects without exposing exact instantiation logic.
```csharp
public interface INotification
{
    Task SendAsync(string message, CancellationToken ct = default);
}

public class EmailNotification : INotification { /* ... */ }
public class SmsNotification : INotification { /* ... */ }

public interface INotificationFactory
{
    INotification Create(NotificationChannel channel);
}
```

### Builder
**Purpose:** Construct complex objects step by step (especially valuable for unit test fixtures).
```csharp
public class PatientBuilder
{
    private string _firstName = "John";
    private string _lastName = "Doe";
    private string _cpr = "0101901234";

    public PatientBuilder WithName(string first, string last)
    {
        _firstName = first;
        _lastName = last;
        return this;
    }

    public Patient Build() => new() { FirstName = _firstName, LastName = _lastName, CprNumber = _cpr };
}
```

---

## Structural Patterns

### Adapter
**Purpose:** Make incompatible interfaces work together.
```csharp
// Third-party legacy client
public class LegacyBillingSoapService
{
    public bool ProcessPaymentInCents(int cents) => true;
}

// Modern interface
public interface IPaymentGateway
{
    Task<bool> ChargeAsync(Money amount, CancellationToken ct = default);
}

// Adapter
public class LegacyBillingAdapter(LegacyBillingSoapService legacyService) : IPaymentGateway
{
    public Task<bool> ChargeAsync(Money amount, CancellationToken ct = default)
    {
        var cents = (int)(amount.Amount * 100);
        return Task.FromResult(legacyService.ProcessPaymentInCents(cents));
    }
}
```

### Decorator
**Purpose:** Add behavior (caching, logging, timing) dynamically without modifying the original implementation.
```csharp
public class CachedPatientRepository(IPatientRepository inner, IMemoryCache cache) : IPatientRepository
{
    public async Task<Patient?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync($"patient_{id}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return inner.GetByIdAsync(id, ct);
        });
    }
}
```

---

## Behavioral Patterns

### Strategy
**Purpose:** Define a family of algorithms and make them interchangeable at runtime.
```csharp
public interface IDiscountStrategy
{
    decimal CalculateDiscount(decimal basePrice);
}

public class SeniorDiscountStrategy : IDiscountStrategy
{
    public decimal CalculateDiscount(decimal basePrice) => basePrice * 0.15m;
}

public class DentalTreatmentCalculator(IDiscountStrategy discountStrategy)
{
    public decimal CalculateFinalPrice(decimal basePrice)
    {
        return basePrice - discountStrategy.CalculateDiscount(basePrice);
    }
}
```

### Command / Mediator (CQRS Lite)
**Purpose:** Encapsulate a request as an object with dedicated handler.
```csharp
public record CreateAppointmentCommand(int PatientId, DateTime ScheduledAt);

public class CreateAppointmentHandler(ApplicationDbContext db)
{
    public async Task<Result<int>> HandleAsync(CreateAppointmentCommand command, CancellationToken ct)
    {
        // Execute business logic
    }
}
```
