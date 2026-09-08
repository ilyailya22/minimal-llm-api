using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MinimalLlm.Ollama;

namespace MinimalLlm.Tests;

/// <summary>Boots the real application with the model host swapped for a stub.</summary>
public sealed class MinimalLlmApp(FakeOllamaHandler handler) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Ollama:BaseUrl", "http://ollama.test");
        builder.UseSetting("Ollama:ChatModel", "test-model");

        builder.ConfigureServices(services =>
            services.AddHttpClient<IOllamaClient, OllamaClient>()
                .ConfigurePrimaryHttpMessageHandler(() => handler));
    }
}
