# Managing Complexity in C# & .NET

## The Two Types of Complexity

### Essential Complexity
Inherent to the problem domain. Cannot be removed, only managed.
- Business rules & domain logic
- Regulatory constraints (HIPAA, dental compliance)
- User requirements

### Accidental Complexity
Introduced by technical solutions. CAN and SHOULD be minimized.
- Poor abstractions & over-engineering
- Unnecessary indirection & boilerplate generic repositories
- Framework ceremony
- Technical debt

**Goal: Minimize accidental complexity while clearly expressing essential complexity.**

---

## Detecting Complexity

### 1. Change Amplification
Small changes require touching many files.
**Symptom:** "To add this field, I need to update 15 files across 5 projects."
**Cause:** Scattered responsibilities, poor abstraction boundaries.

### 2. Cognitive Load
Code is hard to understand, requires holding too much in memory.
**Symptom:** "I need to understand 10 other classes to understand this one."
**Cause:** Tight coupling, hidden dependencies, unclear naming.

### 3. Unknown Unknowns
Behavior is surprising, side effects are hidden.
**Symptom:** "I changed this endpoint, and an unrelated query broke."
**Cause:** Shared mutable state, global static state, implicit side effects.

---

## KISS - Keep It Simple, Silly

> "The simplest solution that works is usually the best."

### How to Apply:
1. Start with the obvious solution
2. Only add complexity when REQUIRED
3. Prefer boring, well-understood C# features (records, LINQ, primary constructors)
4. Question every abstraction

```csharp
// Over-engineered
public class PatientServiceFactoryProvider
{
    private static PatientServiceFactoryProvider? _instance;
    public static PatientServiceFactoryProvider Instance => _instance ??= new();
    public IPatientServiceFactory CreateFactory() => new PatientServiceFactory();
}

// KISS: Direct dependency injection with primary constructor
public class PatientService(ApplicationDbContext dbContext, ILogger<PatientService> logger) : IPatientService
{
    public async Task<PatientDto?> GetPatientAsync(int id, CancellationToken ct) { /* ... */ }
}
```

---

## YAGNI - You Aren't Gonna Need It

> "Don't build features until they're actually needed."

### Warning Signs:
- "We might need this later"
- "It would be nice to have"
- "Just in case"
- "For future extensibility"

```csharp
// YAGNI violation: Building for hypothetical needs
public class Patient
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    // Hypothetical fields never requested:
    public string? FaxNumber { get; set; }
    public string? PagerNumber { get; set; }
    public string? TelexNumber { get; set; }
}

// YAGNI: Only what's needed NOW
public class Patient
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
```

---

## DRY - Don't Repeat Yourself (with The Rule of Three)

> "Every piece of knowledge should have a single, unambiguous representation."

### The Rule of Three:
**Don't extract duplication until you see it THREE times.**
Why? The wrong abstraction is far worse than temporary duplication.

```
Duplication #1 → Leave it
Duplication #2 → Note it, leave it
Duplication #3 → NOW extract it
```

---

## Separation of Concerns

> "Each module or handler should address a single concern."

### Concerns to Separate:
- **Business logic** vs **Infrastructure / Persistence**
- **What** (policy) vs **How** (mechanism)
- **HTTP / Transport** vs **Application Orchestration**

```csharp
// BAD: Mixed concerns in an API endpoint
app.MapPost("/api/orders", async (OrderDto dto, ApplicationDbContext db, SmtpClient smtp) =>
{
    if (string.IsNullOrWhiteSpace(dto.CustomerEmail)) return Results.BadRequest();
    
    var order = new Order { Total = dto.Items.Sum(x => x.Price) };
    db.Orders.Add(order);
    await db.SaveChangesAsync();

    await smtp.SendMailAsync("clinic@domain.com", dto.CustomerEmail, "Confirmed", "Order received");
    return Results.Ok(order);
});

// GOOD: Separated concerns with dedicated Command & Handler
public record CreateOrderCommand(string CustomerEmail, IReadOnlyList<OrderItemDto> Items);

public class CreateOrderHandler(ApplicationDbContext db, IEmailService emailService)
{
    public async Task<Result<OrderDto>> HandleAsync(CreateOrderCommand cmd, CancellationToken ct)
    {
        // Pure domain validation & execution
    }
}
```

---

## Managing Technical Debt

### The Boy Scout Rule:
> "Leave the code better than you found it."

Every time you touch a C# file:
- Improve one small thing (e.g. convert to primary constructor)
- Replace a raw primitive with a strongly typed `readonly record struct`
- Eliminate an `else` branch using an early guard clause
- Add or clarify an xUnit test
