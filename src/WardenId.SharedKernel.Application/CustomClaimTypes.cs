namespace WardenId.SharedKernel.Application;

/// <summary>Non-registered JWT claim names WardenId issues and reads.</summary>
public static class CustomClaimTypes
{
    public const string TenantId = "tenant_id";
    public const string TokenUse = "token_use";
}
