namespace MinimalLlm.Chat;

/// <summary>
/// A chat turn. <paramref name="ConversationId"/> is optional: omit it to start a new
/// conversation, or pass the id returned by a previous call to continue one.
/// </summary>
public sealed record ChatRequest(string Message, string? ConversationId = null);

public sealed record ChatResponse(string Answer, string ConversationId);

/// <summary>One message in a conversation. Role is "user" or "assistant".</summary>
public sealed record ChatMessage(string Role, string Content);

public sealed record ConversationResponse(string ConversationId, IReadOnlyList<ChatMessage> Messages);
