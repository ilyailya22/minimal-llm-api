using Microsoft.Extensions.Caching.Memory;

namespace MinimalLlm.Chat;

/// <summary>
/// In-memory, per-process conversation history with a sliding TTL.
/// Deliberately not a database — see the design notes in the README.
/// </summary>
public sealed class ConversationStore(IMemoryCache cache)
{
    /// <summary>Idle time after which a conversation is forgotten.</summary>
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Cap on retained messages. Every message is re-sent to the model on each turn, so an
    /// uncapped conversation grows the prompt without bound until generation slows to a crawl.
    /// </summary>
    public const int MaxMessages = 40;

    private readonly Lock _gate = new();

    public bool TryGet(string conversationId, out IReadOnlyList<ChatMessage> messages)
    {
        lock (_gate)
        {
            if (cache.TryGetValue(Key(conversationId), out List<ChatMessage>? stored) && stored is not null)
            {
                messages = stored.ToArray();
                return true;
            }
        }

        messages = [];
        return false;
    }

    /// <summary>Returns the history for a conversation plus <paramref name="message"/> appended.</summary>
    public IReadOnlyList<ChatMessage> BuildPrompt(string conversationId, string message)
    {
        TryGet(conversationId, out var history);
        return [.. history, new ChatMessage("user", message)];
    }

    public void Append(string conversationId, string question, string answer)
    {
        lock (_gate)
        {
            var stored = cache.GetOrCreate(Key(conversationId), entry =>
            {
                entry.SlidingExpiration = Ttl;
                return new List<ChatMessage>();
            })!;

            stored.Add(new ChatMessage("user", question));
            stored.Add(new ChatMessage("assistant", answer));

            if (stored.Count > MaxMessages)
            {
                stored.RemoveRange(0, stored.Count - MaxMessages);
            }
        }
    }

    public void Remove(string conversationId)
    {
        lock (_gate)
        {
            cache.Remove(Key(conversationId));
        }
    }

    public static string NewId() => Guid.NewGuid().ToString("n");

    private static string Key(string conversationId) => $"conversation:{conversationId}";
}
