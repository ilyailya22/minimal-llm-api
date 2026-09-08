namespace MinimalLlm.Ollama;

internal sealed record OllamaChatResponse(OllamaMessage? Message);

internal sealed record OllamaMessage(string? Content);

internal sealed record OllamaTagsResponse(IReadOnlyList<OllamaModel>? Models);

internal sealed record OllamaModel(string Name);
