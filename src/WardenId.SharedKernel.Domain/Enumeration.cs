using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace WardenId.SharedKernel.Domain;

public abstract class Enumeration<TEnum>(
    int value,
    string name) : IEquatable<Enumeration<TEnum>> where TEnum : Enumeration<TEnum>
{
    private static readonly Dictionary<int, TEnum> Enumerations = CreateEnumerations();

    public int Value { get; protected init; } = value;
    [MaxLength(100)] public string Name { get; protected init; } = name;

    public static TEnum? FromValue(int value)
    {
        return Enumerations.GetValueOrDefault(value);
    }

    public static TEnum? FromName(string name)
    {
        return Enumerations.Values.FirstOrDefault(x => x.Name == name);
    }

    private static Dictionary<int, TEnum> CreateEnumerations()
    {
        var enumerationType = typeof(TEnum);

        var fieldsForType = enumerationType.GetFields(
                BindingFlags.Public
                | BindingFlags.Static
                | BindingFlags.FlattenHierarchy)
            .Where(fieldInfo =>
                enumerationType.IsAssignableFrom(fieldInfo.FieldType))
            .Select(fieldInfo => (TEnum)fieldInfo.GetValue(default)!);

        return fieldsForType.ToDictionary(x => x.Value);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Value, Name);
    }

    public override string ToString()
    {
        return Name;
    }

    public bool Equals(Enumeration<TEnum>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return GetType() == other.GetType()
               && Value == other.Value
               && Name == other.Name;
    }

    public override bool Equals(object? obj)
    {
        return obj is Enumeration<TEnum> other && Equals(other);
    }
}