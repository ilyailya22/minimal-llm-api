using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MinimalLlm.Ollama;

public sealed class OllamaClient(HttpClient httpClient, IOptions<OllamaOptions> options) : IOllamaClient
{
    private readonly OllamaOptions _options = options.Value;

    public async Task<string> ChatAsync(string message, CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(message, stream: false);

        using var response = await httpClient.PostAsJsonAsync(
            "/api/chat", request, OllamaJsonContext.Default.OllamaChatRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync(
            OllamaJsonContext.Default.OllamaChatResponse, cancellationToken);

        return result?.Message?.Content
               ?? throw new InvalidOperationException("Ollama returned an empty response.");
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        string message,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(
                BuildRequest(message, stream: true), OllamaJsonContext.Default.OllamaChatRequest)
        };

        // ResponseHeadersRead is what makes this a stream: without it HttpClient buffers the
        // whole body first and there is nothing left to stream.
        using var response = await httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(body);

        // Ollama streams NDJSON: one complete JSON object per line.
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length == 0)
            {
                continue;
            }

            var chunk = JsonSerializer.Deserialize(line, OllamaJsonContext.Default.OllamaChatResponse);

            if (chunk?.Message?.Content is { Length: > 0 } fragment)
            {
                yield return fragment;
            }

            if (chunk?.Done == true)
            {
                yield break;
            }
        }
    }

    public async Task<IReadOnlyCollection<string>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("/api/tags", cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync(
            OllamaJsonContext.Default.OllamaTagsResponse, cancellationToken);

        return result?.Models?.Select(x => x.Name).ToArray() ?? [];
    }

    private OllamaChatRequest BuildRequest(string message, bool stream) =>
        new(_options.ChatModel, [new OllamaChatMessage("user", message)], stream);
}
