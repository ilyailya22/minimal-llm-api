namespace MinimalLlm.Ollama;

public interface IOllamaClient
{
    Task<string> ChatAsync(string message, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<string>> GetModelsAsync(CancellationToken cancellationToken = default);
}
