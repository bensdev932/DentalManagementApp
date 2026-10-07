# Test-Driven Development (TDD) in .NET

## The Core Loop

```
RED → GREEN → REFACTOR → RED → ...
```

### RED Phase
Write a failing xUnit test that describes the domain behavior you want:
- Use domain language, not technical jargon
- Describe WHAT, not HOW
- Be a concrete example, not an abstract statement

```csharp
// BAD: Abstract
[Fact]
public void CanAddNumbers() { /* ... */ }

// GOOD: Concrete example
[Fact]
public void Add_WhenAdding2And3_Returns5()
{
    var calculator = new Calculator();
    var result = calculator.Add(2, 3);
    result.Should().Be(5);
}
```

### GREEN Phase
Write the **simplest possible C# code** to make the test pass:

1. **Fake It** - Return a hardcoded value
   ```csharp
   public int Add(int a, int b) => 5; // Simplest thing!
   ```

2. **Obvious Implementation** - If you know the solution
   ```csharp
   public int Add(int a, int b) => a + b;
   ```

**Prefer Fake It** when learning or unsure. Let more tests drive the real implementation.

### REFACTOR Phase
This is where **design happens**. Look for:
- Duplication (wait for Rule of Three)
- Long methods to extract
- Poor names to improve
- Complex conditions to simplify with guard clauses
- Primitive obsession to eliminate with `readonly record struct` value objects

## The Three Laws of TDD

1. **No production code** without a failing test
2. **No more test code** than sufficient to fail (compilation failures count)
3. **No more production code** than sufficient to pass the one failing test

## The Rule of Three

**Only extract duplication when you see it THREE times.**

Why? Wrong abstractions are worse than duplication. Wait for the pattern to emerge.

## Triangulation

Each new test "sculpts" the solution toward a general, robust implementation.
Each test carves out one degree of freedom until the implementation handles all cases.

## Transformation Priority Premise

When going from RED to GREEN, prefer simpler transformations:

| Priority | Transformation |
|----------|----------------|
| 1 | {} → nil/null |
| 2 | null → constant |
| 3 | constant → variable |
| 4 | unconditional → conditional |
| 5 | scalar → collection |
| 6 | statement → recursion/LINQ |
| 7 | value → mutated value |

Higher priority = simpler. Avoid jumping to complex transformations too early.

## Arrange-Act-Assert (AAA) Pattern

Structure every xUnit test:

```csharp
[Fact]
public void CalculateTotal_WithPercentageDiscount_AppliesDiscountAccurately()
{
    // ARRANGE - Set up the world
    var order = new Order();
    order.AddItem(new OrderItem("Toothbrush", 100m));
    var discount = new PercentDiscount(10);

    // ACT - Execute the behavior
    var total = order.CalculateTotal(discount);

    // ASSERT - Verify outcome using FluentAssertions
    total.Should().Be(90m);
}
```

## Writing Tests Backwards

Sometimes it helps to write AAA in reverse:
1. Write the ASSERT first - what do you want to verify?
2. Write the ACT - what action produces that result?
3. Write the ARRANGE - what setup is needed?

## Test Naming Principles

- Use **behavior-driven names** with domain language
- Provide **concrete examples**, not abstract statements
- **One assertion concept per test** for clean diagnostics
- Avoid leaking implementation details

```csharp
// BAD: Technical, implementation-focused
[Fact]
public void SetsTheDataPropertyTo1() { /* ... */ }

// GOOD: Behavior-focused, domain language
[Fact]
public void IsPalindrome_WithMom_ReturnsTrue() { /* ... */ }
```

## Classic vs Mockist TDD

**Classic (Detroit/Chicago) TDD:**
- Test with real domain models and in-memory stores
- Higher confidence, faster refactoring of internals
- Best for: Pure domain logic, algorithms, calculation engines

**Mockist (London) TDD:**
- Mock external dependencies (`Moq`, `NSubstitute`)
- Isolates unit of work cleanly
- Best for: Handlers that orchestrate databases, message buses, email senders

Start with Classic TDD for domain models and value objects. Add mocks when testing handlers with external side effects.

## Common Mistakes

1. **Writing code before tests** - Violates the fundamental principle
2. **Writing too much test** - Just enough to fail
3. **Writing too much code** - Just enough to pass
4. **Skipping refactor** - This is where clean code lives
5. **Testing implementation** - Test behavior and contracts, not internal private fields
6. **Abstract test names** - Use concrete examples
7. **Extracting too early** - Wait for the Rule of Three
