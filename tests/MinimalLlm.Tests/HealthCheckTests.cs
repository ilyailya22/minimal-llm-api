using System.Net;

namespace MinimalLlm.Tests;

public sealed class HealthCheckTests
{
    [Fact]
    public async Task Health_is_ok_when_the_model_host_answers()
    {
        using var app = new MinimalLlmApp(FakeOllamaHandler.Canned());
        using var client = app.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Health_is_unhealthy_when_the_model_host_throws()
    {
        using var app = new MinimalLlmApp(FakeOllamaHandler.Unreachable());
        using var client = app.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Health_is_degraded_when_the_host_has_no_models()
    {
        using var app = new MinimalLlmApp(FakeOllamaHandler.Canned(models: []));
        using var client = app.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Degraded", await response.Content.ReadAsStringAsync());
    }
}
