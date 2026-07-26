using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

public sealed class MfaPolicy : ValueObject
{
    public MfaPolicyMode Mode { get; }

    private MfaPolicy(MfaPolicyMode mode) => Mode = mode;

    public static MfaPolicy Create(MfaPolicyMode mode) => new(mode);

    public static MfaPolicy FromTrustedSource(MfaPolicyMode mode) => new(mode);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Mode;
    }
}
