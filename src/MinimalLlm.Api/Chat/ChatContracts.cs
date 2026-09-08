namespace MinimalLlm.Chat;

public sealed record ChatRequest(string Message);

public sealed record ChatResponse(string Answer);
