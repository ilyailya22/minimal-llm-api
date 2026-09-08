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
            CancellationToken cancellationToken) =>
        {
            if (Validate(request) is { } problem)
            {
                return problem;
            }

            var answer = await ollamaClient.ChatAsync(request.Message, cancellationToken);
            return Results.Ok(new ChatResponse(answer));
        });

        group.MapPost("/chat/stream", (
            ChatRequest request,
            IOllamaClient ollamaClient,
            CancellationToken cancellationToken) =>
        {
            if (Validate(request) is { } problem)
            {
                return problem;
            }

            // cancellationToken is HttpContext.RequestAborted: when the client hangs up it
            // cancels the read of Ollama's response stream, which stops the generation.
            return TypedResults.ServerSentEvents(
                ollamaClient.StreamChatAsync(request.Message, cancellationToken),
                eventType: "token");
        });

        return app;
    }

    private static IResult? Validate(ChatRequest request) =>
        string.IsNullOrWhiteSpace(request.Message)
            ? Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(ChatRequest.Message)] = ["Message cannot be empty."]
            })
            : null;
}
