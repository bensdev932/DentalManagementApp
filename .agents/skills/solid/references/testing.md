# Testing Strategy in .NET & ASP.NET Core

## The Testing Pyramid

```
       /\
      /  \        E2E / Functional Tests (Few)
     /----\       - Full WebApplicationFactory / HTTP pipeline
    /      \      - Real database or Testcontainers
   /--------\
  /          \    Integration Tests (Some)
 /------------\   - EF Core DbContext, repository/handler slice queries
/              \  - Medium speed
----------------
      Unit Tests (Many)
      - Single class, handler, or domain entity
      - Fast, isolated, zero I/O
```

## Test Types

### Unit Tests

Test ONE class, handler, or domain model in isolation.

**Characteristics:**
- Fast (milliseconds)
- No external dependencies (mocked via `Moq`)
- Most of your tests should be unit tests

```csharp
public class OrderTests
{
    [Fact]
    public void CalculateTotal_WithMultipleItems_CalculatesAccurately()
    {
        var order = new Order();
        order.AddItem(new OrderItem("Toothpaste", 100m));
        order.AddItem(new OrderItem("Floss", 50m));

        var total = order.CalculateTotal();

        total.Should().Be(150m);
    }
}
```

### Integration Tests

Test multiple components together (e.g. ASP.NET Core `WebApplicationFactory<Program>` or EF Core database queries).

**Characteristics:**
- Tests real behavior against database or HTTP endpoints
- Confirms LINQ queries translate to SQL correctly

```csharp
public class OrderEndpointIntegrationTests(WebApplicationFactory<Program> factory) 
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task PostOrder_WithValidPayload_ReturnsCreated()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new CreateOrderCommand(1, 150m));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
```

---

## Arrange-Act-Assert (AAA)

Structure EVERY test cleanly:

```csharp
[Fact]
public void CalculateDiscount_ForPremiumPatient_AppliesTwentyPercent()
{
    // ARRANGE - Set up domain state
    var patient = new Patient { IsPremium = true };
    var bill = new Bill(patient);
    bill.AddItem(100m);

    // ACT - Execute behavior
    var total = bill.CalculateTotal();

    // ASSERT - Verify outcome
    total.Should().Be(80m); // 20% discount
}
```

---

## Test Naming Conventions

### Good: Concrete Examples, Domain Language

```csharp
// MethodName_Scenario_ExpectedResult
[Fact]
public void CalculateDiscount_WhenPatientIsPremium_AppliesTwentyPercent() { /* ... */ }

[Fact]
public void RegisterPatient_WhenEmailAlreadyExists_ReturnsConflictResult() { /* ... */ }

[Fact]
public void IsValidCpr_WhenChecksumFails_ReturnsFalse() { /* ... */ }
```

---

## Test Doubles in .NET

### Dummy
Object passed to satisfy parameter lists but never executed:
```csharp
var dummyLogger = Mock.Of<ILogger<PatientService>>();
var service = new PatientService(context, dummyLogger);
```

### Stub
Returns predefined values:
```csharp
var tokenMock = new Mock<ITokenService>();
tokenMock.Setup(s => s.GenerateJwt(It.IsAny<User>())).Returns("valid-jwt-token");
```

### Mock
Verifies expected method calls and invocations:
```csharp
var emailMock = new Mock<IEmailService>();
// Execution...
emailMock.Verify(s => s.SendEmailAsync("patient@example.com", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
```

### Fake
Working in-memory implementation (e.g. SQLite In-Memory or EF Core InMemory provider for rapid tests):
```csharp
var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
    .Options;

using var context = new ApplicationDbContext(options);
```

---

## Testing Strategies by Layer

### Domain Layer (Most Tests)
- Pure unit tests with zero mocks
- Test business rules, domain invariants, `readonly record struct` value objects

```csharp
public class MoneyTests
{
    [Fact]
    public void Add_WhenSameCurrency_ReturnsSummedAmount()
    {
        var a = Money.FromDollars(10m);
        var b = Money.FromDollars(20m);
        var result = a + b;
        result.Amount.Should().Be(30m);
    }

    [Fact]
    public void Add_WhenDifferentCurrencies_ThrowsInvalidOperationException()
    {
        var usd = Money.FromDollars(10m);
        var eur = Money.FromEuros(10m);
        var act = () => usd + eur;
        act.Should().Throw<InvalidOperationException>();
    }
}
```

### Application / Handler Layer
- Test handler orchestration, mapping, and error paths
- Mock external APIs, payment gateways, and email senders

```csharp
[Fact]
public async Task Handle_WhenPatientExists_DeletesPatientAndReturnsSuccess()
{
    // Arrange
    using var context = CreateInMemoryDbContext();
    var patient = new Patient { Id = 1, FirstName = "Jane", LastName = "Doe" };
    context.Patients.Add(patient);
    await context.SaveChangesAsync();

    var handler = new DeletePatientHandler(context, Mock.Of<ILogger<DeletePatientHandler>>());

    // Act
    var result = await handler.HandleAsync(new DeletePatientCommand(1));

    // Assert
    result.Success.Should().BeTrue();
    context.Patients.Should().BeEmpty();
}
```

---

## Common Testing Mistakes

| Mistake | Problem | Solution |
|---------|---------|----------|
| Testing implementation | Brittle tests | Test public behavior and domain contracts |
| Too many mocks | Tests prove nothing | Use real domain models & in-memory DbContext |
| Shared state between tests | Flaky tests | Use fresh DbContext / unique db name per test |
| Missing assertions | False confidence | Always assert explicit expected values |
| Testing trivial auto-properties | Wasted effort | Focus on business rules and edge cases |
