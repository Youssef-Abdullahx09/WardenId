using System.Net;
using WardenId.SharedKernel.FileOperation;

namespace WardenId.Protocol.Presentation.Features.Login;

internal static class LoginPageRenderer
{
    private const string LoginTemplatePath = "Templates/Login.html";
    private const string SuccessTemplatePath = "Templates/LoginSuccess.html";

    public static async Task<string> RenderFormAsync(
        IReadFileService readFileService,
        string errorMessage,
        string antiforgeryFieldName,
        string antiforgeryToken,
        CancellationToken cancellationToken)
    {
        var template = await ReadTemplateAsync(readFileService, LoginTemplatePath, cancellationToken);
        return template
            .Replace("{{ErrorMessage}}", WebUtility.HtmlEncode(errorMessage))
            .Replace("{{AntiforgeryFieldName}}", WebUtility.HtmlEncode(antiforgeryFieldName))
            .Replace("{{AntiforgeryToken}}", WebUtility.HtmlEncode(antiforgeryToken));
    }

    public static async Task<string> RenderSuccessAsync(
        IReadFileService readFileService, string tokenPayloadJson, CancellationToken cancellationToken)
    {
        var template = await ReadTemplateAsync(readFileService, SuccessTemplatePath, cancellationToken);
        return template.Replace("{{TokenPayloadJson}}", WebUtility.HtmlEncode(tokenPayloadJson));
    }

    private static async Task<string> ReadTemplateAsync(
        IReadFileService readFileService, string templatePath, CancellationToken cancellationToken)
    {
        var result = await readFileService.ReadFileContentAsync(templatePath, cancellationToken);
        return result.ValueOrDefault ?? throw new InvalidOperationException($"Template not found: {templatePath}");
    }
}
