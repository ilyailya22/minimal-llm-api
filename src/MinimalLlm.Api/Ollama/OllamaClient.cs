using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MinimalLlm.Chat;

namespace MinimalLlm.Ollama;

public sealed class OllamaClient(
    HttpClient httpClient,
    IOptions<OllamaOptions> options,
    ILogger<OllamaClient> logger) : IOllamaClient
{
    private readonly OllamaOptions _options = options.Value;

    public async Task<string> ChatAsync(
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        var request = BuildRequest(messages, stream: false);

        using var response = await httpClient.PostAsJsonAsync(
            "/api/chat", request, OllamaJsonContext.Default.OllamaChatRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync(
            OllamaJsonContext.Default.OllamaChatResponse, cancellationToken);

        var answer = result?.Message?.Content
                     ?? throw new InvalidOperationException("Ollama returned an empty response.");

        OllamaLog.ChatCompleted(
            logger, _options.ChatModel, messages.Count, PromptLength(messages), answer.Length, ElapsedMs(started));

        return answer;
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        IReadOnlyList<ChatMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        var fragments = 0;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(
                BuildRequest(messages, stream: true), OllamaJsonContext.Default.OllamaChatRequest)
        };

        // ResponseHeadersRead is what makes this a stream: without it HttpClient buffers the
        // whole body first and there is nothing left to stream.
        using var response = await httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(body);

        try
        {
            // Ollama streams NDJSON: one complete JSON object per line.
            while (true)
            {
                var line = await reader.ReadLineAsync(cancellationToken);

                if (line is null)
                {
                    break;
                }

                if (line.Length == 0)
                {
                    continue;
                }

                var chunk = JsonSerializer.Deserialize(line, OllamaJsonContext.Default.OllamaChatResponse);

                if (chunk?.Message?.Content is { Length: > 0 } fragment)
                {
                    fragments++;
                    yield return fragment;
                }

                if (chunk?.Done == true)
                {
                    break;
                }
            }
        }
        finally
        {
            if (cancellationToken.IsCancellationRequested)
            {
                OllamaLog.StreamCancelled(logger, _options.ChatModel, fragments, ElapsedMs(started));
            }
            else
            {
                OllamaLog.StreamCompleted(
                    logger, _options.ChatModel, messages.Count, PromptLength(messages), fragments, ElapsedMs(started));
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

    private OllamaChatRequest BuildRequest(IReadOnlyList<ChatMessage> messages, bool stream) =>
        new(_options.ChatModel,
            [.. messages.Select(m => new OllamaChatMessage(m.Role, m.Content))],
            stream);

    private static int PromptLength(IReadOnlyList<ChatMessage> messages) =>
        messages.Sum(m => m.Content.Length);

    private static long ElapsedMs(long startedTimestamp) =>
        (long)Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds;
}
