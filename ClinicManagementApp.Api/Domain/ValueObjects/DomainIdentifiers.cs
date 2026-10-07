namespace ClinicManagementApp.Api.Domain.ValueObjects;

/// <summary>
/// Strongly-typed domain identifier for a patient.
/// Eliminates primitive obsession and prevents accidental argument transposition.
/// </summary>
public readonly record struct PatientId
{
    public int Value { get; }

    public PatientId(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentException("Patient ID must be a positive integer.", nameof(value));
        }

        Value = value;
    }

    public static implicit operator int(PatientId id) => id.Value;
    public static explicit operator PatientId(int value) => new(value);

    public override string ToString() => Value.ToString();
}

/// <summary>
/// Strongly-typed domain identifier for an orthodontic contract.
/// </summary>
public readonly record struct ContractId
{
    public int Value { get; }

    public ContractId(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentException("Contract ID must be a positive integer.", nameof(value));
        }

        Value = value;
    }

    public static implicit operator int(ContractId id) => id.Value;
    public static explicit operator ContractId(int value) => new(value);

    public override string ToString() => Value.ToString();
}

/// <summary>
/// Strongly-typed domain identifier for a clinical treatment record.
/// </summary>
public readonly record struct TreatmentId
{
    public int Value { get; }

    public TreatmentId(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentException("Treatment ID must be a positive integer.", nameof(value));
        }

        Value = value;
    }

    public static implicit operator int(TreatmentId id) => id.Value;
    public static explicit operator TreatmentId(int value) => new(value);

    public override string ToString() => Value.ToString();
}

/// <summary>
/// Strongly-typed domain identifier for an clinic expense.
/// </summary>
public readonly record struct ExpenseId
{
    public int Value { get; }

    public ExpenseId(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentException("Expense ID must be a positive integer.", nameof(value));
        }

        Value = value;
    }

    public static implicit operator int(ExpenseId id) => id.Value;
    public static explicit operator ExpenseId(int value) => new(value);

    public override string ToString() => Value.ToString();
}

