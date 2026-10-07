# Code Smells & Anti-Patterns in C#

## What Are Code Smells?

Indicators that something MAY be wrong. Not bugs, but design problems that make C# code hard to understand, change, or test.

## The Five Categories

### 1. Bloaters
Code that has grown too large.

| Smell | Symptom | Refactoring |
|-------|---------|-------------|
| **Long Method** | > 15 lines | Extract Method / Local Function |
| **Large Class** | > 100 lines, multiple responsibilities | Extract Class / Vertical Slice Handler |
| **Long Parameter List** | > 3 parameters | Introduce Command / Parameter Record |
| **Data Clumps** | Same group of variables appear together | Introduce Record or Value Object |
| **Primitive Obsession** | Raw `int`, `string` instead of typed IDs | Wrap in `readonly record struct` |

### 2. Object-Orientation Abusers
Misuse of OO principles.

| Smell | Symptom | Refactoring |
|-------|---------|-------------|
| **Switch Statements** | Type checking, large switch/if-else | Pattern Matching / Strategy Pattern |
| **Parallel Inheritance** | Adding subclass requires adding another | Composition over Inheritance |
| **Refused Bequest** | Subclass doesn't use parent methods | Replace Inheritance with Delegation |
| **Alternative Classes** | Different interfaces, same concept | Standardize Interface Contracts |

### 3. Change Preventers
Code that makes changes difficult.

| Smell | Symptom | Refactoring |
|-------|---------|-------------|
| **Divergent Change** | One class changed for many reasons | Extract Handlers (SRP) |
| **Shotgun Surgery** | One change touches many classes | Move Method/Field together |

### 4. Dispensables
Code that can be removed.

| Smell | Symptom | Refactoring |
|-------|---------|-------------|
| **Comments** | Explaining bad code | Rename, Extract Method |
| **Duplicate Code** | Copy-paste | Extract Method (Rule of Three) |
| **Dead Code** | Unreachable code | Delete |
| **Speculative Generality** | "Just in case" code | Delete (YAGNI) |
| **Generic Repository Wrappers** | Wrapping DbContext without added value | Query EF Core LINQ directly |

### 5. Couplers
Excessive coupling between classes.

| Smell | Symptom | Refactoring |
|-------|---------|-------------|
| **Feature Envy** | Method uses another class's data extensively | Move Method |
| **Inappropriate Intimacy** | Classes know too much about each other | Move Method, Encapsulate |
| **Message Chains** | `a.GetB().GetC().GetD()` | Law of Demeter |

---

## The Common Code Smells & Solutions

### 1. Long Method & Multiple Responsibilities

```csharp
// SMELL
public async Task ProcessOrderAsync(Order order)
{
    if (order.Items.Count == 0) throw new InvalidOperationException("Empty");
    decimal total = 0;
    foreach (var item in order.Items)
    {
        total += item.Price * item.Quantity;
    }
    total *= 1.25m; // tax
    _db.Orders.Add(order);
    await _db.SaveChangesAsync();
    await _email.SendAsync(order.Email, "Confirmed");
}

// REFACTORED
public async Task ProcessOrderAsync(Order order, CancellationToken ct)
{
    order.Validate();
    order.CalculateTotalWithTax();
    await SaveOrderAsync(order, ct);
    await NotifyCustomerAsync(order, ct);
}
```

### 2. Primitive Obsession

```csharp
// SMELL: Raw int and string parameters
public async Task<Patient> RegisterAsync(string email, string cpr, decimal balance);

// REFACTORED: Strongly typed domain value objects
public readonly record struct PatientEmail(string Value);
public readonly record struct CprNumber(string Value);
public readonly record struct Money(decimal Amount, string Currency);

public async Task<Patient> RegisterAsync(PatientEmail email, CprNumber cpr, Money balance, CancellationToken ct);
```

### 3. Inappropriate Intimacy

```csharp
// SMELL: Reaching directly into inventory's internal collections
foreach (var item in order.Items)
{
    inventory.StockLevels[item.ProductId] -= item.Quantity;
}

// REFACTORED: Tell, Don't Ask
var reservationResult = inventory.ReserveStock(order.Items);
if (!reservationResult.IsSuccess)
{
    return ApiResponse<bool>.FailureResult("Insufficient stock.");
}
```

---

## Prevention Strategies in .NET

1. **Follow Object Calisthenics** - Guard clauses, no `else`, 1 level of indentation
2. **Practice TDD** - Tests reveal design friction early
3. **Use Primary Constructors** - Reduces boilerplate, keeps classes lean
4. **Use `readonly record struct`** - Eliminates primitive obsession with zero memory overhead
5. **Use EF Core LINQ Idiomatically** - Avoid unnecessary abstraction layers over DbContext
