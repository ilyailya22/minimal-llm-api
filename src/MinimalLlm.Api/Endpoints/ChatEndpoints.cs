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
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(ChatRequest.Message)] = ["Message cannot be empty."]
                });
            }

            var answer = await ollamaClient.ChatAsync(request.Message, cancellationToken);
            return Results.Ok(new ChatResponse(answer));
        });

        return app;
    }
}
