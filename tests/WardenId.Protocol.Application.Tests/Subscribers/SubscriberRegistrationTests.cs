using DotNetCore.CAP;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SessionEnded = WardenId.Protocol.Application.Subscribers.SessionEndedDomainEvent;

namespace WardenId.Protocol.Application.Tests.Subscribers;

public class SubscriberRegistrationTests
{
    // CAP discovers subscribers from the service collection: an unregistered ICapSubscribe class never runs.
    [Fact]
    public void EverySubscriberClass_IsRegisteredForCapDiscovery()
    {
        var services = new ServiceCollection();
        services.AddProtocolApplication(new ConfigurationBuilder().Build());

        var subscriberTypes = typeof(SessionEnded.Subscriber).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(ICapSubscribe).IsAssignableFrom(type))
            .ToList();

        Assert.Equal(7, subscriberTypes.Count);
        Assert.All(subscriberTypes, type =>
            Assert.Contains(services, descriptor =>
                descriptor.ServiceType == typeof(ICapSubscribe)
                && descriptor.ImplementationType == type
                && descriptor.Lifetime == ServiceLifetime.Scoped));
    }
}
