using System.ComponentModel.DataAnnotations;

namespace MinimalLlm.Ollama;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    /// <summary>Base address of the Ollama host. Required — there is no sensible default to fall back to.</summary>
    [Required]
    [Url]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Model tag passed to Ollama, e.g. "llama3.1".</summary>
    [Required]
    public string ChatModel { get; set; } = string.Empty;
}
