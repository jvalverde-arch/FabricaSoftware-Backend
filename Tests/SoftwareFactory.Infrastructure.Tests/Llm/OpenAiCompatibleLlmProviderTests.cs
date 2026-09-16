using System.Net;
using System.Text.Json;
using SoftwareFactory.Application.Common.Llm;
using SoftwareFactory.Application.Finops.Contracts;
using SoftwareFactory.Infrastructure.Llm;

namespace SoftwareFactory.Infrastructure.Tests.Llm;

/// <summary>The OpenAI-compatible adapter covers vLLM and Ollama (doc 03, D3): same port, different wire format.</summary>
public sealed class OpenAiCompatibleLlmProviderTests
{
    private const string Body = """
        {
          "id": "chatcmpl-1",
          "model": "qwen3-8b",
          "choices": [{ "index": 0, "finish_reason": "stop", "message": { "role": "assistant", "content": "Respuesta local." } }],
          "usage": { "prompt_tokens": 90, "completion_tokens": 25, "prompt_tokens_details": { "cached_tokens": 40 } }
        }
        """;

    [Fact]
    public async Task Posts_a_chat_completion_with_the_system_prompt_as_the_first_message()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, Body);
        var provider = Create(handler);

        var response = await provider.CompleteAsync(
            new LlmProviderRequest
            {
                Model = "qwen3-8b",
                System = "Eres un clasificador.",
                Messages = [LlmMessage.User("clasifica esto")],
                MaxOutputTokens = 256,
                Temperature = 0.2m,
            },
            CancellationToken.None);

        Assert.Equal("local", provider.Name);
        Assert.Equal("Respuesta local.", response.Text);
        Assert.Equal("qwen3-8b", response.Model);
        Assert.Equal("stop", response.StopReason);
        Assert.Equal(new LlmUsage(InputTokens: 90, OutputTokens: 25, CacheReadTokens: 40, CacheWriteTokens: 0), response.Usage);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/v1/chat/completions", request.RequestUri?.AbsolutePath);
        Assert.Equal("Bearer local-key", request.Headers.Authorization?.ToString());

        using var sent = JsonDocument.Parse(Assert.Single(handler.Bodies));
        var root = sent.RootElement;
        Assert.Equal("qwen3-8b", root.GetProperty("model").GetString());
        Assert.Equal(256, root.GetProperty("max_tokens").GetInt32());
        Assert.Equal(0.2, root.GetProperty("temperature").GetDouble(), 3);
        var messages = root.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("Eres un clasificador.", messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
    }

    [Fact]
    public async Task Works_against_an_endpoint_without_api_key_and_without_cache_counters()
    {
        const string minimal = """
            {
              "model": "llama3",
              "choices": [{ "message": { "role": "assistant", "content": "ok" } }],
              "usage": { "prompt_tokens": 5, "completion_tokens": 2 }
            }
            """;
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, minimal);
        var provider = new OpenAiCompatibleLlmProvider(
            "local",
            new LlmProviderOptions { Kind = LlmProviderKind.OpenAiCompatible, BaseUrl = new Uri("http://localhost:11434/v1") },
            new HttpClient(handler));

        var response = await provider.CompleteAsync(Request(), CancellationToken.None);

        Assert.Equal("ok", response.Text);
        Assert.Equal(new LlmUsage(InputTokens: 5, OutputTokens: 2, CacheReadTokens: 0, CacheWriteTokens: 0), response.Usage);
        Assert.Null(Assert.Single(handler.Requests).Headers.Authorization);
    }

    [Fact]
    public async Task An_error_response_becomes_an_exception_with_the_status()
    {
        var provider = Create(new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable, """{"error":{"message":"overloaded"}}"""));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => provider.CompleteAsync(Request(), CancellationToken.None));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
    }

    [Fact]
    public async Task An_answer_without_choices_is_rejected()
    {
        var provider = Create(new StubHttpMessageHandler(HttpStatusCode.OK, """{"model":"x","choices":[],"usage":{"prompt_tokens":1,"completion_tokens":0}}"""));

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.CompleteAsync(Request(), CancellationToken.None));
    }

    private static LlmProviderRequest Request() => new()
    {
        Model = "qwen3-8b",
        Messages = [LlmMessage.User("hola")],
        MaxOutputTokens = 128,
    };

    private static OpenAiCompatibleLlmProvider Create(StubHttpMessageHandler handler) =>
        new(
            "local",
            new LlmProviderOptions
            {
                Kind = LlmProviderKind.OpenAiCompatible,
                BaseUrl = new Uri("https://llm.local/v1"),
                ApiKey = "local-key",
            },
            new HttpClient(handler));
}
