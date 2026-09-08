using System.Net;
using System.Net.Http.Json;
using MinimalLlm.Chat;

namespace MinimalLlm.Tests;

public sealed class ConversationTests
{
    [Fact]
    public async Task A_second_turn_sends_the_earlier_messages_to_the_model()
    {
        var handler = FakeOllamaHandler.Canned(answer: "Yes.");
        using var app = new MinimalLlmApp(handler);
        using var client = app.CreateClient();

        var first = await client.PostAsJsonAsync("/api/chat", new ChatRequest("How do indexes work?"));
        var firstBody = await first.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(firstBody);

        await client.PostAsJsonAsync(
            "/api/chat", new ChatRequest("What did I just ask?", firstBody.ConversationId));

        // Turn 1 sends the question alone; turn 2 sends question, answer, follow-up.
        Assert.Equal(2, handler.ChatRequests.Count);
        Assert.DoesNotContain("What did I just ask?", handler.ChatRequests[0]);
        Assert.Contains("How do indexes work?", handler.ChatRequests[1]);
        Assert.Contains("Yes.", handler.ChatRequests[1]);
        Assert.Contains("What did I just ask?", handler.ChatRequests[1]);
    }

    [Fact]
    public async Task History_is_returned_then_cleared_by_delete()
    {
        using var app = new MinimalLlmApp(FakeOllamaHandler.Canned(answer: "Yes."));
        using var client = app.CreateClient();

        var chat = await client.PostAsJsonAsync("/api/chat", new ChatRequest("Hello?"));
        var body = await chat.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(body);

        var history = await client.GetFromJsonAsync<ConversationResponse>(
            $"/api/conversations/{body.ConversationId}");
        Assert.NotNull(history);
        Assert.Equal(
            [new ChatMessage("user", "Hello?"), new ChatMessage("assistant", "Yes.")],
            history.Messages);

        var deleted = await client.DeleteAsync($"/api/conversations/{body.ConversationId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var gone = await client.GetAsync($"/api/conversations/{body.ConversationId}");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task An_unknown_conversation_id_is_a_404_problem_document()
    {
        using var app = new MinimalLlmApp(FakeOllamaHandler.Canned());
        using var client = app.CreateClient();

        var response = await client.GetAsync("/api/conversations/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_cancelled_stream_is_not_written_to_the_history()
    {
        // A stream that never finishes, so the only way out is the client hanging up.
        var handler = new FakeOllamaHandler((_, _, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new SlowNdjsonStream())
            }));

        using var app = new MinimalLlmApp(handler);
        using var client = app.CreateClient();
        using var cts = new CancellationTokenSource();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat/stream")
        {
            Content = JsonContent.Create(new ChatRequest("go"))
        };

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        var conversationId = Assert.Single(response.Headers.GetValues("X-Conversation-Id"));

        // Read a little, then hang up mid-generation.
        await using var body = await response.Content.ReadAsStreamAsync(cts.Token);
        var buffer = new byte[64];
#pragma warning disable CA2022 // a short read is the point: we only want to start the stream
        _ = await body.ReadAsync(buffer, cts.Token);
#pragma warning restore CA2022
        await cts.CancelAsync();
        response.Dispose();

        var history = await WaitForConversation(client, conversationId);

        Assert.Equal(HttpStatusCode.NotFound, history);
    }

    /// <summary>Gives the aborted server-side request a moment to unwind before asserting.</summary>
    private static async Task<HttpStatusCode> WaitForConversation(HttpClient client, string conversationId)
    {
        HttpStatusCode status = default;

        for (var attempt = 0; attempt < 20; attempt++)
        {
            await Task.Delay(25);
            status = (await client.GetAsync($"/api/conversations/{conversationId}")).StatusCode;

            if (status == HttpStatusCode.NotFound)
            {
                break;
            }
        }

        return status;
    }
}
