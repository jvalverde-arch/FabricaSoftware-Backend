using System.Text.Json;
using SoftwareFactory.Application.Platform.Contracts;

namespace SoftwareFactory.Application.Platform.Handlers;

/// <summary>
/// Job type of sprint 0 (T-007): walks a few phases so the progress stream can be seen end to end before any real
/// agent exists. The payload may ask for a different number of steps, a pause between them, or a deliberate failure
/// to exercise the retries: <c>{"steps":3,"delayMs":200,"fail":false}</c>.
/// </summary>
public sealed class ProbeJobHandler(TimeProvider clock) : IJobHandler
{
    public const string Type = "probe";

    private static readonly string[] _phases = ["leyendo contexto", "generando", "validando"];

    public string JobType => Type;

    public async Task HandleAsync(JobExecution execution, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(execution);

        var options = Parse(execution.Payload);

        for (var step = 0; step < options.Steps; step++)
        {
            var phase = _phases[step % _phases.Length];
            var percent = (int)Math.Round((step + 1) * 100d / options.Steps, MidpointRounding.AwayFromZero);

            await execution.ReportProgressAsync(phase, Math.Min(percent, 99), cancellationToken).ConfigureAwait(false);

            if (options.DelayMs > 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(options.DelayMs), clock, cancellationToken).ConfigureAwait(false);
            }
        }

        if (options.Fail)
        {
            throw new InvalidOperationException("El job de prueba falló a propósito.");
        }
    }

    private static ProbeOptions Parse(string payload)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<ProbeOptions>(payload, JsonSerializerOptions.Web);
            return parsed is null ? new ProbeOptions() : parsed with { Steps = Math.Clamp(parsed.Steps, 1, 20) };
        }
        catch (JsonException)
        {
            return new ProbeOptions();
        }
    }

    private sealed record ProbeOptions
    {
        public int Steps { get; init; } = 3;

        public int DelayMs { get; init; }

        public bool Fail { get; init; }
    }
}
