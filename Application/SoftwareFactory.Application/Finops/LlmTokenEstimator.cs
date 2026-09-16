using SoftwareFactory.Application.Common.Llm;

namespace SoftwareFactory.Application.Finops;

/// <summary>
/// Rough token count used only for the pre-flight budget check, never for billing: billing uses what the provider
/// reports. Four characters per token is the usual approximation for Latin-script text.
/// </summary>
public static class LlmTokenEstimator
{
    private const int CharactersPerToken = 4;

    public static int Estimate(string? system, IReadOnlyList<LlmMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var characters = (system?.Length ?? 0) + messages.Sum(message => message.Content.Length);
        return (characters / CharactersPerToken) + messages.Count;
    }
}
