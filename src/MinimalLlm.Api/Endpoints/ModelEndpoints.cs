using MinimalLlm.Ollama;

namespace MinimalLlm.Endpoints;

public static class ModelEndpoints
{
    public static IEndpointRouteBuilder MapModelEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api");

        group.MapGet("/models", async (
            IOllamaClient ollamaClient,
            CancellationToken cancellationToken) =>
        {
            var models = await ollamaClient.GetModelsAsync(cancellationToken);
            return Results.Ok(models);
        });

        return app;
    }
}
