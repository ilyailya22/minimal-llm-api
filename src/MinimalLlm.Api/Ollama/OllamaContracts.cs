using System.Text.Json.Serialization;

namespace MinimalLlm.Ollama;

// Wire format for Ollama's HTTP API. Property names are camel-cased by OllamaJsonContext.

internal sealed record OllamaChatRequest(
    string Model,
    IReadOnlyList<OllamaChatMessage> Messages,
    bool Stream);

internal sealed record OllamaChatMessage(string Role, string Content);

internal sealed record OllamaChatResponse(OllamaChatMessage? Message);

internal sealed record OllamaTagsResponse(IReadOnlyList<OllamaModel>? Models);

internal sealed record OllamaModel(string Name);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(OllamaChatRequest))]
[JsonSerializable(typeof(OllamaChatResponse))]
[JsonSerializable(typeof(OllamaTagsResponse))]
internal sealed partial class OllamaJsonContext : JsonSerializerContext;
