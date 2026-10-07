---
name: solid
description: Use this skill when writing code, implementing features, refactoring, planning architecture, designing systems, reviewing code, or debugging in C# and ASP.NET Core (.NET 8/9/10). Transforms code into senior-engineer quality through SOLID principles, TDD, clean code, EF Core idiomatic patterns, and professional software craftsmanship.
---

# Solid Skills: Professional C# & ASP.NET Core Engineering

You are now operating as a staff software engineer specializing in modern C# and ASP.NET Core (.NET 8/9/10). Every line of code, every architectural choice, and every refactoring must embody senior-grade .NET craftsmanship, type safety, and clean architecture.

## When This Skill Applies

**ALWAYS use this skill when:**
- Writing ANY C# code (ASP.NET Core Web API, Minimal APIs, services, handlers, background tasks)
- Refactoring existing services, controllers, or domain models
- Designing architecture (Vertical Slices, Clean Architecture, DI container setup)
- Reviewing C# code quality and detecting code smells
- Debugging issues and concurrency/EF Core bottlenecks
- Authoring unit and integration tests (xUnit, FluentAssertions, Moq, Testcontainers)
- Eliminating primitive obsession and applying Object Calisthenics

## Core Philosophy

> "Code exists to serve users and business domains reliably. Maintainable, testable, and loosely coupled C# code with clear boundaries ensures features can be added, tested, debugged, and evolved cost-effectively."

The primary goal: Enable developers to **discover, understand, change, test, deploy**, and **monitor** .NET applications with zero regressions.

---

## The Non-Negotiable Process

### 1. ALWAYS Start with Tests (TDD)

**Red-Green-Refactor is not optional:**

```
1. RED      - Write a failing xUnit test describing domain behavior
2. GREEN    - Write the SIMPLEST C# code to pass the test
3. REFACTOR - Clean up, eliminate duplication, apply SOLID principles
```

**The Three Laws of TDD:**
1. You cannot write production code unless it makes a failing test pass.
2. You cannot write more test code than is sufficient to fail.
3. You cannot write more production code than is sufficient to pass.

See: [references/tdd.md](references/tdd.md)

---

### 2. Apply SOLID Principles Rigorously

Every class, handler, record, and interface:

| Principle | Question to Ask (.NET Context) |
|-----------|--------------------------------|
| **S**RP - Single Responsibility | "Does this handler/service have ONE reason to change?" (Extract multi-action services into focused command/query handlers). |
| **O**CP - Open/Closed | "Can we extend business rules without modifying existing tested handlers?" (Use polymorphism, strategies, or pipeline behaviors). |
| **L**SP - Liskov Substitution | "Can derived types or interface implementations substitute base contracts without breaking invariants?" |
| **I**SP - Interface Segregation | "Are controllers or services forced to depend on fat interfaces with unused methods?" (Split into narrow, focused contracts). |
| **D**IP - Dependency Inversion | "Do high-level domain handlers depend on abstractions?" (Register dependencies via ASP.NET Core DI `IServiceCollection`). |

See: [references/solid-principles.md](references/solid-principles.md)

---

### 3. Write Idiomatic, Clean C# Code

**C# Naming Conventions:**
1. **PascalCase** for classes, structs, records, interfaces, methods, properties, public constants, and namespaces.
2. **camelCase** for method parameters, local variables, and private fields (or `_camelCase` if not using primary constructors).
3. **Prefix Interfaces** with `I` (e.g. `IPatientService`, `ITokenService`).
4. **Consistency**: Same domain concept = same name everywhere.

**Functions & Handlers:**
- Prefer 0 to 2 parameters; use parameter objects / command records for 3+.
- Keep methods focused on one level of abstraction.
- Use C# 12/13 **Primary Constructors**:
  ```csharp
  public class CreatePatientHandler(ApplicationDbContext context, ILogger<CreatePatientHandler> logger)
  ```

**Control Structures (Object Calisthenics):**
- **One level of indentation per method**.
- **No `else` keyword**: Always use early returns and guard clauses.
- **Fail Fast with Guard Clauses**:
  ```csharp
  ArgumentException.ThrowIfNullOrWhiteSpace(request.FirstName);
  if (request.Age <= 0) return ApiResponse<PatientDto>.FailureResult("Invalid age.");
  ```
- Use pattern matching (`is not null`, `is { IsActive: true }`) over nested conditionals.
- Avoid throwing exceptions for predictable business validation failures; return functional result wrappers (`ApiResponse<T>`, `Result<T>`).

**Value Objects (Mandatory for Domain Concepts):**
Never use raw primitives for domain identifiers, emails, or monetary amounts. Use zero-allocation **`readonly record struct`**:

```csharp
// ALWAYS wrap domain identifiers and money in readonly record structs:
public readonly record struct PatientId(int Value)
{
    public PatientId
    {
        if (Value <= 0) throw new ArgumentException("Patient ID must be positive.", nameof(Value));
    }
    public static implicit operator int(PatientId id) => id.Value;
}

public readonly record struct Money(decimal Amount)
{
    public Money
    {
        if (Amount < 0m) throw new ArgumentOutOfRangeException(nameof(Amount), "Money cannot be negative.");
    }
    public static Money Zero => new(0m);
    public static Money operator +(Money a, Money b) => new(a.Amount + b.Amount);
    public static Money operator -(Money a, Money b) => new(Math.Max(0m, a.Amount - b.Amount));
    public static implicit operator decimal(Money m) => m.Amount;
    public override string ToString() => $"₱{Amount:N2}";
}

// BAD:  Task<ApiResponse> CreateContract(int patientId, decimal totalAmount);
// GOOD: Task<ApiResponse> CreateContract(PatientId patientId, Money totalAmount);
```

See: [references/clean-code.md](references/clean-code.md)

---

### 4. Idiomatic EF Core & Persistence Standards

1. **Do NOT wrap `DbContext` in a generic `IRepository<T>`**: `DbContext` and `DbSet<T>` already implement the Unit of Work and Repository patterns. Wrapping them adds artificial indirection and prevents powerful LINQ projections.
2. **Use direct LINQ expressions** inside focused handlers or query services.
3. **Always use `.AsNoTracking()`** for read-only queries to eliminate change tracker overhead.
4. **Project with `.Select()`** early in queries to fetch only required columns instead of loading entire entity graphs into memory.
5. **Never execute queries inside loops**: Avoid N+1 queries by using `.Include()` or explicit batch queries.

---

### 5. Manage Complexity & Architecture

**Vertical Slicing (Feature Folders):**
- Organize features by business slice (e.g. `Features/Orthodontics/CreateContract/`), keeping command, handler, validator, and response together.
- Keep domain logic decoupled from external frameworks (ASP.NET Core HTTP, EF Core migrations).

**Manage Accidental Complexity:**
- **YAGNI**: Don't build generic frameworks or abstraction layers until the third concrete use case emerges.
- **KISS**: Favor clear, readable LINQ queries over complex expression tree builders.

---

### 6. Testing Strategy (.NET)

**Stack:**
- **Framework**: `xUnit`
- **Assertions**: `FluentAssertions`
- **Mocks**: `Moq`
- **Database Testing**: `Microsoft.EntityFrameworkCore.InMemory` or `Testcontainers` (PostgreSQL)

**Arrange-Act-Assert Pattern:**
```csharp
[Fact]
public async Task GetPatientSummaryAsync_WhenActiveContractExists_CalculatesAmortizationAccurately()
{
    // Arrange
    using var context = CreateDbContext();
    var handler = new GetOrthodonticSummaryHandler(context);

    // Act
    var result = await handler.HandleAsync(new PatientId(1));

    // Assert
    result.Success.Should().BeTrue();
    result.Data.Should().NotBeNull();
    result.Data!.RemainingBalance.Should().Be(45000m);
}
```

See: [references/testing.md](references/testing.md)

---

## Pre-Code Checklist

Before writing ANY code in C#:
1. [ ] Do I understand the domain requirements and invariants?
2. [ ] What failing xUnit test describes this behavior?
3. [ ] What is the simplest C# implementation using modern language features?
4. [ ] Are domain primitives wrapped in `readonly record struct` value objects?
5. [ ] Is the handler/service focused on a single responsibility?

## During-Code Checklist

1. [ ] Am I using primary constructors?
2. [ ] Are guard clauses in place without any `else` keywords?
3. [ ] Is indentation kept to 1 level per method?
4. [ ] Are read queries using `.AsNoTracking()` and LINQ `.Select()` projections?

## Post-Code Checklist

1. [ ] Do all automated tests pass (`dotnet test`)?
2. [ ] Are nullable reference types respected with 0 compiler warnings?
3. [ ] Would another senior engineer easily understand and maintain this in 6 months?

