using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using WardenId.Protocol.Application.Features.Authorize.SignIn;

namespace WardenId.Protocol.Presentation.Tests;

public class PendingSignInTests
{
    private static readonly Guid Acme = Guid.NewGuid();
    private static readonly Guid Globex = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-10-16T08:00:00Z"));
    private readonly IServiceProvider _services;

    public PendingSignInTests()
    {
        var services = new ServiceCollection();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddSingleton<TimeProvider>(_clock);
        _services = services.BuildServiceProvider();
    }

    private HttpContext Request(string path, string? cookie = null)
    {
        var context = new DefaultHttpContext { RequestServices = _services };
        context.Request.Path = path;
        if (cookie is not null) context.Request.Headers.Cookie = $"{PendingSignIn.CookieName}={cookie}";
        return context;
    }

    private string IssueFor(Guid tenantId, string path = "/tenants/acme/login")
    {
        var context = Request(path);
        PendingSignIn.Issue(context, PendingSignIn.Create(
            context, tenantId, UserId, [SignInStep.SecondFactor, SignInStep.EnrollTotp], "/tenants/acme/authorize?x=1", ["pwd"]));
        var setCookie = context.Response.Headers.SetCookie.Single()!;
        return setCookie[(PendingSignIn.CookieName.Length + 1)..setCookie.IndexOf(';')];
    }

    [Fact]
    public void Read_RoundTripsTheTicket_ForItsTenant()
    {
        var cookie = IssueFor(Acme);

        var ticket = PendingSignIn.Read(Request("/tenants/acme/login/code", cookie), Acme);

        Assert.NotNull(ticket);
        Assert.Equal(UserId, ticket.UserId);
        Assert.Equal([SignInStep.SecondFactor, SignInStep.EnrollTotp], ticket.Steps);
        Assert.Equal(SignInStep.SecondFactor, ticket.NextStep);
        Assert.Equal("/tenants/acme/authorize?x=1", ticket.ReturnUrl);
        Assert.Equal(["pwd"], ticket.Amr);
    }

    [Fact]
    public void Read_RejectsATicketOfAnotherTenant() =>
        Assert.Null(PendingSignIn.Read(Request("/tenants/globex/login/code", IssueFor(Acme)), Globex));

    [Fact]
    public void Read_RejectsAnExpiredTicket()
    {
        var cookie = IssueFor(Acme);

        _clock.Advance(PendingSignIn.Lifetime - TimeSpan.FromSeconds(1));
        Assert.NotNull(PendingSignIn.Read(Request("/tenants/acme/login/code", cookie), Acme));

        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Null(PendingSignIn.Read(Request("/tenants/acme/login/code", cookie), Acme));
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("")]
    public void Read_RejectsATamperedOrEmptyCookie(string value) =>
        Assert.Null(PendingSignIn.Read(Request("/tenants/acme/login/code", value), Acme));

    [Fact]
    public void Read_RejectsAFlippedByte()
    {
        var cookie = IssueFor(Acme).ToCharArray();
        cookie[20] = cookie[20] == 'A' ? 'B' : 'A';

        Assert.Null(PendingSignIn.Read(Request("/tenants/acme/login/code", new string(cookie)), Acme));
    }

    [Fact]
    public void Issue_ScopesTheCookieToTheTenantPath_AndLimitsItsAge()
    {
        var context = Request("/tenants/acme/login");
        PendingSignIn.Issue(context, PendingSignIn.Create(context, Acme, UserId, [SignInStep.SecondFactor], null, ["pwd"]));

        var setCookie = context.Response.Headers.SetCookie.Single()!;
        Assert.Contains("path=/tenants/acme", setCookie);
        Assert.Contains("secure", setCookie);
        Assert.Contains("httponly", setCookie);
        Assert.Contains("samesite=lax", setCookie);
        Assert.Contains("max-age=300", setCookie);
    }

    [Fact]
    public void Advance_DropsTheDoneStep_KeepsTheRest_AndRestartsTheClock()
    {
        var context = Request("/tenants/acme/login/code");
        var ticket = PendingSignIn.WithEnrollment(
            context,
            PendingSignIn.Create(context, Acme, UserId, [SignInStep.SecondFactor, SignInStep.EnrollTotp], null, ["pwd"]),
            new PendingTotpEnrollment(Guid.NewGuid(), "SECRET", "otpauth://totp/x"));
        _clock.Advance(TimeSpan.FromMinutes(3));

        var advanced = PendingSignIn.Advance(context, ticket, ["pwd", "otp", "mfa"]);

        Assert.Equal([SignInStep.EnrollTotp], advanced.Steps);
        Assert.Equal(["pwd", "otp", "mfa"], advanced.Amr);
        Assert.Null(advanced.Enrollment);
        Assert.Equal(_clock.GetUtcNow() + PendingSignIn.Lifetime, advanced.ExpiresAt);
    }
}
