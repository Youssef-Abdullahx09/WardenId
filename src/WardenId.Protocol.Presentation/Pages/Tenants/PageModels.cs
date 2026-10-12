namespace WardenId.Protocol.Presentation.Pages.Tenants;

/// <summary>Posts the original authorize parameters back with cancel=true → access_denied at the RP.</summary>
public sealed record ReturnToApplication(string Slug, IReadOnlyList<KeyValuePair<string, string>> Parameters, string Label);

public sealed record SignInBlockedView(string Message, ReturnToApplication? Back);
