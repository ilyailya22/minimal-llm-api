using MinimalLlm.Chat;

namespace MinimalLlm.Ollama;

public interface IOllamaClient
{
    /// <summary>Sends the whole conversation so the model has context, and waits for the answer.</summary>
    Task<string> ChatAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>As <see cref="ChatAsync"/>, but yields the answer fragment by fragment as it is produced.</summary>
    IAsyncEnumerable<string> StreamChatAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<string>> GetModelsAsync(CancellationToken cancellationToken = default);
}
