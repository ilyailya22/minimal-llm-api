using System.Net;
using System.Net.Http.Json;
using MinimalLlm.Chat;

namespace MinimalLlm.Tests;

public sealed class ChatEndpointTests
{
    [Fact]
    public async Task Chat_returns_the_models_answer_and_a_conversation_id()
    {
        using var app = new MinimalLlmApp(FakeOllamaHandler.Canned(answer: "B-trees stay balanced."));
        using var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chat", new ChatRequest("How do indexes work?"));
        var body = await response.Content.ReadFromJsonAsync<ChatResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("B-trees stay balanced.", body.Answer);
        Assert.False(string.IsNullOrWhiteSpace(body.ConversationId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Chat_rejects_an_empty_message_with_a_problem_document(string message)
    {
        using var app = new MinimalLlmApp(FakeOllamaHandler.Canned());
        using var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chat", new ChatRequest(message));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();
        Assert.NotNull(problem);
        Assert.Contains("Message", problem.Errors.Keys);
    }

    [Fact]
    public async Task Models_lists_what_the_host_has_pulled()
    {
        using var app = new MinimalLlmApp(FakeOllamaHandler.Canned(models: ["llama3.2:1b", "phi4"]));
        using var client = app.CreateClient();

        var models = await client.GetFromJsonAsync<string[]>("/api/models");

        Assert.NotNull(models);
        Assert.Equal(["llama3.2:1b", "phi4"], models);
    }

    private sealed record ValidationProblem(Dictionary<string, string[]> Errors);
}
