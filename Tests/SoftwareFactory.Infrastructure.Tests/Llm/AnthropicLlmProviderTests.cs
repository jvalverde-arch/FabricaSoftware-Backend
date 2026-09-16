using System.Net;
using System.Text.Json;
using SoftwareFactory.Application.Common.Llm;
using SoftwareFactory.Application.Finops.Contracts;
using SoftwareFactory.Infrastructure.Llm;

namespace SoftwareFactory.Infrastructure.Tests.Llm;

/// <summary>The Anthropic adapter speaks the Messages API through the official SDK and reports its token counts.</summary>
public sealed class AnthropicLlmProviderTests
{
    private const string Body = """
        {
          "id": "msg_01",
          "type": "message",
          "role": "assistant",
          "model": "claude-sonnet-5",
          "content": [{ "type": "text", "text": "Como usuario quiero iniciar sesión." }],
          "stop_reason": "end_turn",
          "usage": {
            "input_tokens": 1200,
            "output_tokens": 350,
            "cache_read_input_tokens": 800,
            "cache_creation_input_tokens": 64
          }
        }
        """;

    [Fact]
    public async Task Sends_the_model_system_prompt_and_conversation_and_maps_the_usage()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, Body);
        var provider = Create(handler);

        var response = await provider.CompleteAsync(
            new LlmProviderRequest
            {
                Model = "claude-sonnet-5",
                System = "Eres un analista funcional.",
                Messages = [LlmMessage.User("Escribe una historia de usuario"), LlmMessage.Assistant("Claro"), LlmMessage.User("Sobre login")],
                MaxOutputTokens = 2_048,
            },
            CancellationToken.None);

        Assert.Equal("anthropic", provider.Name);
        Assert.Equal("Como usuario quiero iniciar sesión.", response.Text);
        Assert.Equal("claude-sonnet-5", response.Model);
        Assert.Equal("end_turn", response.StopReason);
        Assert.Equal(new LlmUsage(InputTokens: 1_200, OutputTokens: 350, CacheReadTokens: 800, CacheWriteTokens: 64), response.Usage);

        using var sent = JsonDocument.Parse(Assert.Single(handler.Bodies));
        var root = sent.RootElement;
        Assert.Equal("claude-sonnet-5", root.GetProperty("model").GetString());
        Assert.Equal(2_048, root.GetProperty("max_tokens").GetInt32());
        Assert.Equal(3, root.GetProperty("messages").GetArrayLength());
        Assert.Equal("user", root.GetProperty("messages")[0].GetProperty("role").GetString());
        Assert.Equal("assistant", root.GetProperty("messages")[1].GetProperty("role").GetString());
        Assert.Contains("analista funcional", root.GetProperty("system").ToString(), StringComparison.Ordinal);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith("/v1/messages", request.RequestUri?.AbsolutePath ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Joins_every_text_block_of_the_answer()
    {
        const string twoBlocks = """
            {
              "id": "msg_02", "type": "message", "role": "assistant", "model": "claude-sonnet-5",
              "content": [{ "type": "text", "text": "Primera parte." }, { "type": "text", "text": "Segunda parte." }],
              "stop_reason": "end_turn",
              "usage": { "input_tokens": 10, "output_tokens": 20 }
            }
            """;
        var provider = Create(new StubHttpMessageHandler(HttpStatusCode.OK, twoBlocks));

        var response = await provider.CompleteAsync(Request(), CancellationToken.None);

        Assert.Equal("Primera parte.\nSegunda parte.", response.Text);
        Assert.Equal(new LlmUsage(InputTokens: 10, OutputTokens: 20, CacheReadTokens: 0, CacheWriteTokens: 0), response.Usage);
    }

    [Fact]
    public async Task A_provider_error_surfaces_instead_of_being_swallowed()
    {
        var provider = Create(new StubHttpMessageHandler(HttpStatusCode.TooManyRequests, """{"type":"error","error":{"type":"rate_limit_error","message":"slow down"}}"""));

        await Assert.ThrowsAnyAsync<Exception>(() => provider.CompleteAsync(Request(), CancellationToken.None));
    }

    private static LlmProviderRequest Request() => new()
    {
        Model = "claude-sonnet-5",
        Messages = [LlmMessage.User("hola")],
        MaxOutputTokens = 512,
    };

    private static AnthropicLlmProvider Create(StubHttpMessageHandler handler) =>
        new(
            "anthropic",
            new LlmProviderOptions { ApiKey = "sk-ant-test", BaseUrl = new Uri("https://api.anthropic.test") },
            new HttpClient(handler));
}
