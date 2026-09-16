namespace SoftwareFactory.Application.Common.Llm;

/// <summary>One turn of the conversation sent to a model. Provider-neutral by design (doc 03, D3).</summary>
public sealed record LlmMessage(LlmRole Role, string Content)
{
    public static LlmMessage User(string content) => new(LlmRole.User, content);

    public static LlmMessage Assistant(string content) => new(LlmRole.Assistant, content);
}
