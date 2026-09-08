using Microsoft.Extensions.Diagnostics.HealthChecks;
using MinimalLlm.Ollama;

namespace MinimalLlm.Health;

/// <summary>
/// The API is only as available as the model host behind it, so /health asks Ollama for its
/// model list rather than just reporting that this process is running.
/// </summary>
public sealed class OllamaHealthCheck(IOllamaClient ollamaClient) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var models = await ollamaClient.GetModelsAsync(cancellationToken);

            return models.Count > 0
                ? HealthCheckResult.Healthy($"Ollama reachable, {models.Count} model(s) available.")
                : HealthCheckResult.Degraded("Ollama is reachable but has no models pulled.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Ollama is unreachable.", ex);
        }
    }
}
