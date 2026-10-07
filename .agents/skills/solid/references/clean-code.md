# Clean Code Practices in C# & .NET

> Parts of this guide are based on concepts from the **"Clean Code" course summary** by [Academind GmbH / Maximilian Schwarzmuller](https://academind.com) (c) 2020.

## What is Clean Code?

Code that is:
- **Easy to understand** - reveals domain intent clearly
- **Easy to change** - modifications are localized to vertical slices
- **Easy to test** - dependencies are injected, business rules isolated
- **Simple** - zero unnecessary abstractions or premature layers

## The Human-Centered Approach

Code has THREE consumers:
1. **Users** - get their needs met reliably
2. **Customers** - business goals achieved
3. **Developers** - must maintain and evolve it

**Remember: developers read code 10x more than they write it.**

---

## Naming Principles

### 1. Consistency & Uniqueness (HIGHEST PRIORITY)
Same concept = same name everywhere. One name per concept.

```csharp
// BAD: Inconsistent names for same concept
GetPatientById(id);
FetchCustomerById(id);
RetrieveClientById(id);

// GOOD: Consistent
GetPatient(id);
GetAppointment(id);
GetTreatment(id);
```

### 2. Understandability
Use domain language, not technical storage jargon.

```csharp
// BAD: Technical
var arr = list.Where(x => x.Active).ToList();

// GOOD: Domain language
var activePatients = patients.Where(p => p.IsActive).ToList();
```

### 3. Specificity
Avoid vague names: `data`, `info`, `manager`, `processor`, `utils`.

```csharp
// BAD: Vague
public class DataManager { }
public void ProcessInfo(object data) { }

// GOOD: Specific
public class PatientBillingCalculator { }
public void ValidatePayment(PaymentDto payment) { }
```

### 4. Brevity (without sacrificing clarity)
```csharp
// BAD: Too cryptic
var ptnLst = GetPtns();

// BAD: Unnecessarily long
var listOfAllActivePatientsInTheDentalClinicSystem = GetActivePatients();

// GOOD: Brief and clear
var activePatients = GetActivePatients();
```

---

## Functions & Handlers

### Minimize Parameters
The fewer parameters, the easier to read and test:
- 0 to 2 parameters: Ideal
- 3+ parameters: Group into a parameter object or C# `record` (e.g. `CreatePatientCommand`)

```csharp
// BAD: Long positional parameter list
public void CreatePatient(string first, string last, string email, string cpr, DateTime dob, string phone);

// GOOD: Strongly typed command record
public record CreatePatientCommand(
    string FirstName, 
    string LastName, 
    string Email, 
    string CprNumber, 
    DateTime DateOfBirth, 
    string PhoneNumber);
```

### Keep Methods Small & Focused (One Level of Abstraction)
All operations in a method body should be on the same level of abstraction, exactly one level below the method name.

```csharp
// BAD: Mixed levels of abstraction (HTTP + SQL + formatting)
public async Task RegisterPatientAsync(CreatePatientRequest req)
{
    var sql = "INSERT INTO patients ...";
    // raw formatting, raw I/O
}

// GOOD: Consistent abstraction level
public async Task<PatientDto> RegisterPatientAsync(CreatePatientCommand cmd, CancellationToken ct)
{
    ValidateCommand(cmd);
    var patient = MapToPatient(cmd);
    await PersistPatientAsync(patient, ct);
    return MapToDto(patient);
}
```

---

## Control Structures

### Prefer Positive Checks & Early Guards
```csharp
// GOOD: Fail-fast guard clause
if (request.Amount <= 0)
{
    return ApiResponse<PaymentResult>.FailureResult("Amount must be greater than zero.");
}
```

### Avoid Deep Nesting (Object Calisthenics)
- **Maximum 1 level of indentation per method**
- **Do not use the `else` keyword** - use guard clauses and early returns

```csharp
// BAD: Nested conditionals
public decimal CalculateDiscount(Patient patient)
{
    if (patient != null)
    {
        if (patient.IsSenior)
        {
            return 0.15m;
        }
        else
        {
            return 0.05m;
        }
    }
    return 0m;
}

// GOOD: Flattened with guard clauses and no 'else'
public decimal CalculateDiscount(Patient? patient)
{
    if (patient is null) return 0m;
    if (patient.IsSenior) return 0.15m;
    return 0.05m;
}
```

---

## Classes & Objects

### Object Calisthenics Rules in .NET:
1. **One level of indentation per method**
2. **Do not use the `else` keyword**
3. **Wrap all primitives and strings** (use `readonly record struct`)
4. **First-class collections** (wrap complex collection manipulation in dedicated types)
5. **One dot per line (Law of Demeter)** - Avoid chaining `a.B.C.D`
6. **Do not abbreviate**
7. **Keep entities small** (< 100-150 lines)
8. **No classes with more than two to three instance fields** (use composition)
9. **No raw getters/setters with public mutation** - Encapsulate behavior

---

## Comments & Storytelling

### Write Code Like a Story:
1. Public methods / endpoints at the top
2. Private helpers ordered below as they are called
3. Comments should explain **WHY** (domain reason, legacy constraint), never **WHAT** (the C# code already explains what).
