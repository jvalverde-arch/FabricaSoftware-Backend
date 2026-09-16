using SoftwareFactory.Application.Common.Llm;

namespace SoftwareFactory.Application.Tests.Finops.Fakes;

internal sealed class FakeLlmProvider(string name) : ILlmProvider
{
    public string Name { get; } = name;

    public List<LlmProviderRequest> Requests { get; } = [];

    public LlmUsage Usage { get; set; } = new(InputTokens: 1_000, OutputTokens: 500, CacheReadTokens: 0, CacheWriteTokens: 0);

    public string Text { get; set; } = "respuesta del modelo";

    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    public Exception? Throws { get; set; }

    public Func<LlmProviderRequest, Task>? OnCall { get; set; }

    public async Task<LlmProviderResponse> CompleteAsync(LlmProviderRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);

        if (OnCall is not null)
        {
            await OnCall(request);
        }

        if (Throws is not null)
        {
            throw Throws;
        }

        return new LlmProviderResponse(Text, request.Model, Usage, StopReason: "end_turn");
    }
}
