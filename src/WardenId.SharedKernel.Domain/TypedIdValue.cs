namespace WardenId.SharedKernel.Domain;

public abstract class TypedIdValue<TId>(TId value) : ValueObject
{
    public TId Value { get; } = value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString()
    {
        return (Value?.ToString() ?? base.ToString()) ?? throw new InvalidOperationException();
    }
}