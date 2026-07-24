using DotNetCore.CAP;

namespace WardenId.SharedKernel.Application.CapPublisher;

[AttributeUsage(AttributeTargets.Method)]
public sealed class CapSubscriberAttribute : CapSubscribeAttribute
{
    public CapSubscriberAttribute(string name, bool isPartial = false) : base(name, isPartial)
    {
        Group = name;
    }
}