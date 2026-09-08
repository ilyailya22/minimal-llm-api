using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using MinimalLlm;
using MinimalLlm.Chat;
using MinimalLlm.Endpoints;
using MinimalLlm.Health;
using MinimalLlm.Ollama;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Fail at startup, not at the first request, when Ollama is misconfigured.
builder.Services.AddOptions<OllamaOptions>()
    .BindConfiguration(OllamaOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddProblemDetails();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ConversationStore>();
builder.Services.AddOpenApi();

builder.Services.AddHttpClient<IOllamaClient, OllamaClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<OllamaOptions>>().Value;

    client.BaseAddress = new Uri(options.BaseUrl);
    // Generation on a local model is slow, and a streamed answer can run for minutes. This
    // is a backstop against a wedged host, not a per-request deadline — that is the caller's
    // CancellationToken, which flows all the way into the response stream read.
    client.Timeout = TimeSpan.FromMinutes(10);
});

builder.Services.AddHealthChecks()
    .AddCheck<OllamaHealthCheck>("ollama", tags: ["ready"]);

// A local model serves one or two generations at a time; more in parallel makes every one of
// them slower rather than serving more callers. Queue the rest instead of thrashing the host.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddConcurrencyLimiter(RateLimitPolicies.Generation, limiter =>
    {
        limiter.PermitLimit = 2;
        limiter.QueueLimit = 8;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRateLimiter();

app.MapOpenApi();
app.MapScalarApiReference();

app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { status = "ok" }));

app.MapChatEndpoints();
app.MapModelEndpoints();
app.MapConversationEndpoints();

app.Run();

/// <summary>Exposed so the test project can boot the real application with WebApplicationFactory.</summary>
public partial class Program;
