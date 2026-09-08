using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
using MinimalLlm.Chat;
using MinimalLlm.Ollama;

namespace MinimalLlm.Endpoints;

public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api");

        group.MapPost("/chat", async (
            ChatRequest request,
            IOllamaClient ollamaClient,
            ConversationStore conversations,
            CancellationToken cancellationToken) =>
        {
            if (Validate(request) is { } problem)
            {
                return problem;
            }

            var conversationId = request.ConversationId ?? ConversationStore.NewId();
            var prompt = conversations.BuildPrompt(conversationId, request.Message);

            var answer = await ollamaClient.ChatAsync(prompt, cancellationToken);
            conversations.Append(conversationId, request.Message, answer);

            return Results.Ok(new ChatResponse(answer, conversationId));
        });

        group.MapPost("/chat/stream", (
            ChatRequest request,
            IOllamaClient ollamaClient,
            ConversationStore conversations,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            if (Validate(request) is { } problem)
            {
                return problem;
            }

            var conversationId = request.ConversationId ?? ConversationStore.NewId();

            // The id has to reach the client before the tokens do, so a new conversation can be
            // continued even if the caller never reads to the end of the stream.
            http.Response.Headers["X-Conversation-Id"] = conversationId;

            return TypedResults.ServerSentEvents(
                Stream(request.Message, conversationId, ollamaClient, conversations, cancellationToken));
        });

        group.RequireRateLimiting(RateLimitPolicies.Generation);

        return app;
    }

    private static async IAsyncEnumerable<SseItem<string>> Stream(
        string message,
        string conversationId,
        IOllamaClient ollamaClient,
        ConversationStore conversations,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new SseItem<string>(conversationId, eventType: "conversation");

        var prompt = conversations.BuildPrompt(conversationId, message);
        var answer = new StringBuilder();

        await foreach (var fragment in ollamaClient.StreamChatAsync(prompt, cancellationToken))
        {
            answer.Append(fragment);
            yield return new SseItem<string>(fragment, eventType: "token");
        }

        // Only reached when generation ran to completion: a client hang-up throws out of the
        // loop above, and a half-generated answer is not worth remembering.
        conversations.Append(conversationId, message, answer.ToString());
    }

    private static IResult? Validate(ChatRequest request) =>
        string.IsNullOrWhiteSpace(request.Message)
            ? Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(ChatRequest.Message)] = ["Message cannot be empty."]
            })
            : null;
}
