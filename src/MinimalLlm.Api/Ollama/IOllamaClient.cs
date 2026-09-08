namespace MinimalLlm.Ollama;

public interface IOllamaClient
{
    Task<string> ChatAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>Streams the answer token by token as Ollama produces it.</summary>
    IAsyncEnumerable<string> StreamChatAsync(string message, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<string>> GetModelsAsync(CancellationToken cancellationToken = default);
}
