namespace MinimalLlm.Ollama;

public sealed class OllamaClient : IOllamaClient
{
    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;

    public OllamaClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _options = configuration
            .GetSection(OllamaOptions.SectionName)
            .Get<OllamaOptions>() ?? new OllamaOptions();
    }

    public async Task<string> ChatAsync(string message, CancellationToken cancellationToken = default)
    {
        var request = new
        {
            model = _options.ChatModel,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = message
                }
            },
            stream = false
        };

        using var response = await _httpClient.PostAsJsonAsync("/api/chat", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: cancellationToken);

        return result?.Message?.Content
               ?? throw new InvalidOperationException("Ollama returned empty response.");
    }

    public async Task<IReadOnlyCollection<string>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(cancellationToken: cancellationToken);

        return result?.Models?.Select(x => x.Name).ToArray()
               ?? [];
    }
}
