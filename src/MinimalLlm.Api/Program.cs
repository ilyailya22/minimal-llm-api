using Microsoft.Extensions.Options;
using MinimalLlm.Endpoints;
using MinimalLlm.Ollama;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Fail at startup, not at the first request, when Ollama is misconfigured.
builder.Services.AddOptions<OllamaOptions>()
    .BindConfiguration(OllamaOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.Services.AddHttpClient<IOllamaClient, OllamaClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<OllamaOptions>>().Value;

    client.BaseAddress = new Uri(options.BaseUrl);
    // Generation on a local model is slow; this is not a hung request.
    client.Timeout = TimeSpan.FromMinutes(3);
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();
app.MapScalarApiReference();

app.MapGet("/", () => Results.Ok(new { status = "ok" }));

app.MapChatEndpoints();
app.MapModelEndpoints();

app.Run();
