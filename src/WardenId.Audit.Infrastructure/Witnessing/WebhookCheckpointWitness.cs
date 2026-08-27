using System.Text;
using Microsoft.Extensions.Options;
using WardenId.Audit.Application.Abstractions;

namespace WardenId.Audit.Infrastructure.Witnessing;

/// <summary>
/// Spec §8's "a webhook per checkpoint" witness. Only registered when
/// AuditWitnessOptions.WebhookUrl is configured. Posts the plain signed-note text (spec §6's
/// interop format) as the request body — no vendor-specific envelope, per §2's "leave extension
/// points, build no [SIEM] adapters."
/// </summary>
public sealed class WebhookCheckpointWitness(HttpClient httpClient, IOptions<AuditWitnessOptions> options) : IAuditCheckpointWitness
{
    public async Task WitnessAsync(string signedNoteText, CancellationToken cancellationToken = default)
    {
        var url = options.Value.WebhookUrl
            ?? throw new InvalidOperationException("WebhookCheckpointWitness requires WebhookUrl to be configured.");

        using var content = new StringContent(signedNoteText, Encoding.UTF8, "text/plain");
        var response = await httpClient.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
