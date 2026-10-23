namespace WardenId.Identity.Application.Utilities;

/// <summary>Placeholders in the email templates (<see cref="EmailTemplatePaths"/>); each must match the template text.</summary>
public static class EmailPlaceholderKeys
{
    public const string Username = "{{Username}}";
    public const string VerificationCode = "{{VerificationCode}}";
    public const string ResetCode = "{{ResetCode}}";
    public const string ExpiresAtUtc = "{{ExpiresAtUtc}}";
}
