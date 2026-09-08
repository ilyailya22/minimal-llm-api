using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using MinimalLlm.Chat;
using MinimalLlm.Ollama;

namespace MinimalLlm.Tests;

public sealed class StreamingTests
{
    [Fact]
    public async Task Stream_sends_each_fragment_as_its_own_event_in_order()
    {
        using var app = new MinimalLlmApp(FakeOllamaHandler.Streaming("Rows ", "align ", "in ", "silence."));
        using var client = app.CreateClient();

        var body = await PostStream(client, new ChatRequest("haiku"));
        var tokens = TokensFrom(body);

        Assert.Equal(["Rows ", "align ", "in ", "silence."], tokens);
    }

    [Fact]
    public async Task Stream_announces_the_conversation_id_before_the_first_token()
    {
        using var app = new MinimalLlmApp(FakeOllamaHandler.Streaming("hi"));
        using var client = app.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat/stream")
        {
            Content = JsonContent.Create(new ChatRequest("hello"))
        };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        var body = await response.Content.ReadAsStringAsync();

        var headerId = Assert.Single(response.Headers.GetValues("X-Conversation-Id"));
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToArray();

        Assert.Equal("event: conversation", lines[0]);
        Assert.Equal($"data: {headerId}", lines[1]);
    }

    [Fact]
    public async Task Cancelling_stops_the_enumeration_partway_through()
    {
        // A handler that never signals "done" and pauses between lines, so cancellation is the
        // only thing that can end the stream.
        var handler = new FakeOllamaHandler((_, _, _) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StreamContent(new SlowNdjsonStream())
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://ollama.test") };
        var client = new OllamaClient(
            httpClient,
            Options.Create(new OllamaOptions { BaseUrl = "http://ollama.test", ChatModel = "test-model" }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<OllamaClient>.Instance);

        using var cts = new CancellationTokenSource();
        var received = new List<string>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var fragment in client.StreamChatAsync([new ChatMessage("user", "go")], cts.Token))
            {
                received.Add(fragment);

                if (received.Count == 3)
                {
                    await cts.CancelAsync();
                }
            }
        });

        // Stopped where it was told to, rather than draining an endless stream.
        Assert.Equal(3, received.Count);
    }

    private static async Task<string> PostStream(HttpClient client, ChatRequest request)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/chat/stream")
        {
            Content = JsonContent.Create(request)
        };
        using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>The "data: " payloads of events whose preceding "event:" line is "token".</summary>
    internal static string[] TokensFrom(string sse)
    {
        var lines = sse.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        var tokens = new List<string>();

        for (var i = 0; i < lines.Length - 1; i++)
        {
            if (lines[i] == "event: token" && lines[i + 1].StartsWith("data: ", StringComparison.Ordinal))
            {
                tokens.Add(lines[i + 1]["data: ".Length..]);
            }
        }

        return [.. tokens];
    }
}
