namespace MinimalLlm.Ollama;

/// <summary>
/// Request-level logging for calls to the model host. Prompt CONTENT is deliberately never
/// logged — only its size — so logs stay useful without becoming a transcript of what users ask.
/// </summary>
internal static partial class OllamaLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Chat completed: model={Model} messages={MessageCount} promptChars={PromptLength} answerChars={AnswerLength} elapsedMs={ElapsedMs}")]
    public static partial void ChatCompleted(
        ILogger logger, string model, int messageCount, int promptLength, int answerLength, long elapsedMs);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Stream completed: model={Model} messages={MessageCount} promptChars={PromptLength} fragments={Fragments} elapsedMs={ElapsedMs}")]
    public static partial void StreamCompleted(
        ILogger logger, string model, int messageCount, int promptLength, int fragments, long elapsedMs);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Stream cancelled by caller after {Fragments} fragment(s) and {ElapsedMs}ms: model={Model}")]
    public static partial void StreamCancelled(ILogger logger, string model, int fragments, long elapsedMs);
}
