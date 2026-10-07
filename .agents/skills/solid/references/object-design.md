# Object-Oriented Design in C# & .NET

## Responsibility-Driven Design (RDD)

The key insight: **Objects are defined by their responsibilities, not their data.**

### Finding Responsibilities
Each object should answer:
- What does this object **know**?
- What does this object **do**?
- What does this object **decide**?

### Object Stereotypes

| Stereotype | Purpose | .NET Example |
|------------|---------|--------------|
| **Information Holder** | Knows things, holds data | `Patient`, `Appointment`, `Address` |
| **Structurer** | Maintains relationships | `List<OrderItem>`, `DentalRecordHistory` |
| **Service Provider** | Performs work | `TokenService`, `EmailNotificationSender` |
| **Coordinator / Handler** | Orchestrates workflow | `CreatePatientHandler`, `BookAppointmentHandler` |
| **Controller / Endpoint**| Maps HTTP to domain | `PatientsController`, Minimal API Endpoint |
| **Interfacer** | Transforms between systems | `SupabaseSyncService`, `DatabaseMapper` |

---

## Tell, Don't Ask

**Command objects to do work. Don't interrogate their state and do the work yourself.**

```csharp
// BAD: Asking, then mutating
if (account.Balance >= amount)
{
    account.Balance -= amount;
}

// GOOD: Telling
var result = account.Withdraw(amount);
if (result.IsSuccess)
{
    // Proceed
}
```

---

## Composition Over Inheritance

**Prefer composing objects over extending classes.**

```csharp
// BAD: Rigid Inheritance
public class PremiumPatient : Patient
{
    public decimal GetDiscount() => 0.20m;
}

// GOOD: Composition / Strategy
public interface IDiscountPolicy
{
    decimal Calculate(decimal amount);
}

public class Patient(IDiscountPolicy discountPolicy)
{
    public decimal CalculateTotal(decimal baseAmount) => discountPolicy.Calculate(baseAmount);
}
```

---

## The Law of Demeter (Principle of Least Knowledge)

**Only talk to your immediate friends.**

```csharp
// BAD: Train wreck reach-through
var city = order.Customer.Address.City;

// GOOD: Tell the immediate friend
var city = order.GetShippingCity();
```

---

## Encapsulation

**Hide internal details, expose explicit domain behavior.**

```csharp
// BAD: Exposed public mutable state
public class Order
{
    public List<OrderItem> Items { get; set; } = [];
    public decimal Total { get; set; }
}

// GOOD: Encapsulated
public class Order
{
    private readonly List<OrderItem> _items = [];
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();
    public decimal Total { get; private set; }

    public void AddItem(OrderItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _items.Add(item);
        Total += item.Price;
    }
}
```

---

## Value Objects vs Entities in .NET

### Value Objects
- Defined by their attributes (no identity)
- Immutable
- Zero-allocation `readonly record struct` in modern C#:

```csharp
public readonly record struct Money(decimal Amount, string Currency)
{
    public static Money FromDollars(decimal amount) => new(amount, "USD");

    public static Money operator +(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new InvalidOperationException($"Currency mismatch: {a.Currency} vs {b.Currency}");
        return new(a.Amount + b.Amount, a.Currency);
    }
}

public readonly record struct PatientId(int Value)
{
    public static implicit operator int(PatientId id) => id.Value;
    public static explicit operator PatientId(int value) => new(value);
}
```

### Entities
- Defined by unique identity (e.g. `Id`)
- State mutations happen through domain methods, not public setters:

```csharp
public class Patient
{
    public PatientId Id { get; init; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;

    public void UpdateName(string firstName, string lastName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
        FirstName = firstName;
        LastName = lastName;
    }
}
```

---

## Aggregates

A cluster of domain objects treated as a single unit for data changes.

- One object is the **Aggregate Root** (entry point).
- External code only references and saves the root.
- Root enforces invariants for the entire cluster.

```csharp
public class Appointment
{
    public int Id { get; private set; }
    public PatientId PatientId { get; private set; }
    public DateTime ScheduledAt { get; private set; }
    public AppointmentStatus Status { get; private set; }

    public void Confirm()
    {
        if (Status != AppointmentStatus.Pending)
            throw new InvalidOperationException("Only pending appointments can be confirmed.");
        Status = AppointmentStatus.Confirmed;
    }
}
```
