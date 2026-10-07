using System.Globalization;

namespace ClinicManagementApp.Api.Domain.ValueObjects;

/// <summary>
/// Domain value object representing monetary values in the clinic management domain.
/// Guarantees non-negative monetary figures and provides safe domain arithmetic and formatting.
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    private static readonly CultureInfo PhCulture = new("en-PH");

    public decimal Amount { get; }

    public Money(decimal amount)
    {
        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Monetary amount cannot be negative.");
        }

        Amount = amount;
    }

    public static Money Zero => new(0m);

    public bool IsZero => Amount == 0m;

    public Money Round(int decimals = 2) => new(Math.Round(Amount, decimals, MidpointRounding.AwayFromZero));

    public static Money operator +(Money left, Money right) => new(left.Amount + right.Amount);

    public static Money operator -(Money left, Money right)
    {
        var result = left.Amount - right.Amount;
        return new Money(Math.Max(0m, result));
    }

    public static Money operator *(Money money, decimal multiplier)
    {
        if (multiplier < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Multiplier cannot be negative.");
        }

        return new Money(money.Amount * multiplier);
    }

    public static Money operator /(Money money, int divisor)
    {
        if (divisor <= 0)
        {
            throw new DivideByZeroException("Divisor must be greater than zero.");
        }

        return new Money(money.Amount / divisor);
    }

    public static bool operator <(Money left, Money right) => left.Amount < right.Amount;
    public static bool operator <=(Money left, Money right) => left.Amount <= right.Amount;
    public static bool operator >(Money left, Money right) => left.Amount > right.Amount;
    public static bool operator >=(Money left, Money right) => left.Amount >= right.Amount;

    public static implicit operator decimal(Money money) => money.Amount;
    public static explicit operator Money(decimal amount) => new(amount);

    public int CompareTo(Money other) => Amount.CompareTo(other.Amount);

    public override string ToString() => string.Format(PhCulture, "₱{0:N2}", Amount);
}

