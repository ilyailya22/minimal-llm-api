using System.Net;
using System.Text;
using System.Text.Json;

namespace MinimalLlm.Tests;

/// <summary>
/// Stands in for the Ollama host. Stubbing at the HttpMessageHandler seam is what keeps the
/// suite runnable with no model installed and no container started.
/// </summary>
public sealed class FakeOllamaHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, string, CancellationToken, Task<HttpResponseMessage>> _respond;

    /// <summary>Bodies of the /api/chat requests received, in order — the assertion surface for context.</summary>
    public List<string> ChatRequests { get; } = [];

    public FakeOllamaHandler(
        Func<HttpRequestMessage, string, CancellationToken, Task<HttpResponseMessage>> respond) =>
        _respond = respond;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);

        if (request.RequestUri?.AbsolutePath == "/api/chat")
        {
            lock (ChatRequests)
            {
                ChatRequests.Add(body);
            }
        }

        return await _respond(request, body, cancellationToken);
    }

    /// <summary>Answers /api/tags with a model list and /api/chat with a single non-streamed reply.</summary>
    public static FakeOllamaHandler Canned(
        string answer = "42",
        IEnumerable<string>? models = null) =>
        new((request, _, _) =>
        {
            var response = request.RequestUri?.AbsolutePath switch
            {
                "/api/tags" => Json(new
                {
                    models = (models ?? ["llama3.2:1b", "qwen2.5:0.5b"]).Select(m => new { name = m }).ToArray()
                }),
                "/api/chat" => Json(new { message = new { role = "assistant", content = answer }, done = true }),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };

            return Task.FromResult(response);
        });

    /// <summary>Answers /api/chat with NDJSON, one line per fragment, as a real stream would.</summary>
    public static FakeOllamaHandler Streaming(params string[] fragments) =>
        new((request, _, cancellationToken) =>
        {
            if (request.RequestUri?.AbsolutePath == "/api/tags")
            {
                return Task.FromResult(Json(new { models = new[] { new { name = "llama3.2:1b" } } }));
            }

            var ndjson = new StringBuilder();

            foreach (var fragment in fragments)
            {
                ndjson.Append(JsonSerializer.Serialize(
                    new { message = new { role = "assistant", content = fragment }, done = false })).Append('\n');
            }

            ndjson.Append(JsonSerializer.Serialize(
                new { message = new { role = "assistant", content = "" }, done = true })).Append('\n');

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ndjson.ToString(), Encoding.UTF8, "application/x-ndjson")
            });
        });

    /// <summary>Every request throws, as if the model host were not running.</summary>
    public static FakeOllamaHandler Unreachable() =>
        new((_, _, _) => throw new HttpRequestException("Connection refused."));

    private static HttpResponseMessage Json(object payload) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
}
