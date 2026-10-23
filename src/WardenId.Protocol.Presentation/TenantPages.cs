using System.Xml;
using Microsoft.AspNetCore.WebUtilities;
using WardenId.Protocol.Application;

namespace WardenId.Protocol.Presentation;

/// <summary>Local URLs of a tenant's sign-in and account pages.</summary>
public static class TenantPages
{
    /// <summary>Razor Pages root folder: the pages live beside the endpoints under Features/.</summary>
    public const string RootDirectory = "/Features";

    /// <summary>The folder of the tenant sign-in and account pages, relative to <see cref="RootDirectory"/>.</summary>
    public const string Folder = "/Tenants";

    /// <summary>Fields the pages post to the authorize and end-session endpoints, beside the protocol parameters.</summary>
    public static class FormFields
    {
        public const string Confirm = "confirm";
        public const string Cancel = "cancel";
    }

    public static class QueryParameters
    {
        public const string ReturnUrl = "ReturnUrl";
        public const string Cancelled = "cancelled";
        public const string UseRecoveryCode = "UseRecoveryCode";
    }

    public static string Login(string slug) => ProtocolPaths.Tenant(slug, ProtocolPaths.Pages.Login);

    /// <summary>The login page, coming back to <paramref name="returnUrl"/> afterwards.</summary>
    public static string Login(string slug, string returnUrl) =>
        $"{Login(slug)}?{QueryParameters.ReturnUrl}={Uri.EscapeDataString(returnUrl)}";

    /// <summary>The second-factor step; with <paramref name="useRecoveryCode"/>, its recovery-code form.</summary>
    public static string LoginCode(string slug, bool useRecoveryCode)
    {
        var path = ProtocolPaths.Tenant(slug, ProtocolPaths.Pages.LoginCode);
        return useRecoveryCode
            ? QueryHelpers.AddQueryString(path, QueryParameters.UseRecoveryCode, XmlConvert.ToString(true))
            : path;
    }

    /// <summary>The confirmation page after the user cancelled an RP-initiated sign-out.</summary>
    public static string LogoutCancelled(string slug) =>
        QueryHelpers.AddQueryString(
            ProtocolPaths.Tenant(slug, ProtocolPaths.Pages.Logout), QueryParameters.Cancelled, XmlConvert.ToString(true));

    public static string Account(string slug) => ProtocolPaths.Tenant(slug, ProtocolPaths.Pages.Account);
}
