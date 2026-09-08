using MinimalLlm.Chat;

namespace MinimalLlm.Endpoints;

public static class ConversationEndpoints
{
    public static IEndpointRouteBuilder MapConversationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/conversations");

        group.MapGet("/{id}", (string id, ConversationStore conversations) =>
            conversations.TryGet(id, out var messages)
                ? Results.Ok(new ConversationResponse(id, messages))
                : Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Conversation not found.",
                    detail: $"No conversation '{id}'. Conversations are forgotten after {ConversationStore.Ttl.TotalMinutes:0} minutes idle."));

        group.MapDelete("/{id}", (string id, ConversationStore conversations) =>
        {
            conversations.Remove(id);
            return Results.NoContent();
        });

        return app;
    }
}
