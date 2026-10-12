using WardenId.SharedKernel.Presentation;

namespace WardenId.Protocol.Presentation.Tests;

public class RetiredLoginTests
{
    // The Gateway maps every IEndpoint in *.Presentation by reflection, so "no type" ⇒ "no route".
    [Fact]
    public void DirectTokenLoginEndpoint_IsGone()
    {
        var endpoints = typeof(Features.Token.Endpoint).Assembly.GetTypes()
            .Where(t => typeof(IEndpoint).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => t.FullName!)
            .ToArray();

        Assert.DoesNotContain(endpoints, name => name.Contains(".Features.Login.", StringComparison.Ordinal));
        Assert.Contains("WardenId.Protocol.Presentation.Features.Authorize.Endpoint", endpoints);
    }
}
