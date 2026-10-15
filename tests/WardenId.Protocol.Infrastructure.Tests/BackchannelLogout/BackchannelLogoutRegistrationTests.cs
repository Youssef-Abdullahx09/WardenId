using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WardenId.Protocol.Application.Features.BackchannelLogout;
using WardenId.Protocol.Infrastructure.BackchannelLogout;

namespace WardenId.Protocol.Infrastructure.Tests.BackchannelLogout;

/// <summary>Builds the production AddProtocolBackchannelLogout registration (no Postgres, no CAP runtime).</summary>
public class BackchannelLogoutRegistrationTests
{
    private static ServiceProvider Build(Dictionary<string, string?> settings) =>
        new ServiceCollection()
            .AddLogging()
            .AddProtocolBackchannelLogout(new ConfigurationBuilder().AddInMemoryCollection(settings).Build())
            .BuildServiceProvider();

    [Fact]
    public void Timeout_DefaultsToFiveSeconds()
    {
        using var services = Build([]);

        Assert.Equal(
            TimeSpan.FromSeconds(5),
            services.GetRequiredService<IOptions<ProtocolBackchannelLogoutOptions>>().Value.Timeout);
    }

    [Fact]
    public void Timeout_BindsFromProtocolBackchannelLogoutSection()
    {
        using var services = Build(new() { ["ProtocolBackchannelLogout:Timeout"] = "00:00:02" });

        Assert.Equal(
            TimeSpan.FromSeconds(2),
            services.GetRequiredService<IOptions<ProtocolBackchannelLogoutOptions>>().Value.Timeout);
    }

    [Fact]
    public void Timeout_MustBePositive()
    {
        using var services = Build(new() { ["ProtocolBackchannelLogout:Timeout"] = "00:00:00" });

        Assert.Throws<OptionsValidationException>(() =>
            services.GetRequiredService<IOptions<ProtocolBackchannelLogoutOptions>>().Value);
    }

    [Fact]
    public async Task SendAsync_RedirectResponse_IsRejected_AndNotFollowed()
    {
        var landingHits = 0;
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        await using var relyingParty = builder.Build();
        relyingParty.MapPost("/bcl", () => Results.Redirect("/landing"));
        relyingParty.MapMethods("/landing", ["GET", "POST"], () =>
        {
            Interlocked.Increment(ref landingHits);
            return Results.Ok();
        });
        await relyingParty.StartAsync();

        // The production registration, so this pins the real primary handler (AllowAutoRedirect = false).
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddProtocolBackchannelLogout(new ConfigurationBuilder().Build())
            .BuildServiceProvider();
        var sender = services.GetRequiredService<IBackchannelLogoutSender>();

        var outcome = await sender.SendAsync(new Uri(new Uri(relyingParty.Urls.Single()), "/bcl"), "t", CancellationToken.None);

        Assert.Equal(DeliveryOutcome.Rejected, outcome);
        Assert.Equal(0, landingHits);
    }
}
