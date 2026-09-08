using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace MinimalLlm.Ollama;

public sealed class OllamaClient(HttpClient httpClient, IOptions<OllamaOptions> options) : IOllamaClient
{
    private readonly OllamaOptions _options = options.Value;

    public async Task<string> ChatAsync(string message, CancellationToken cancellationToken = default)
    {
        var request = new OllamaChatRequest(
            _options.ChatModel,
            [new OllamaChatMessage("user", message)],
            Stream: false);

        using var response = await httpClient.PostAsJsonAsync(
            "/api/chat", request, OllamaJsonContext.Default.OllamaChatRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync(
            OllamaJsonContext.Default.OllamaChatResponse, cancellationToken);

        return result?.Message?.Content
               ?? throw new InvalidOperationException("Ollama returned an empty response.");
    }

    public async Task<IReadOnlyCollection<string>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("/api/tags", cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync(
            OllamaJsonContext.Default.OllamaTagsResponse, cancellationToken);

        return result?.Models?.Select(x => x.Name).ToArray() ?? [];
    }
}
