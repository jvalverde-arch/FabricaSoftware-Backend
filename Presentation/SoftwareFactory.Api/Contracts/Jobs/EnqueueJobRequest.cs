namespace SoftwareFactory.Api.Contracts.Jobs;

/// <summary>Queues a run. <c>Payload</c> is the JSON document the handler receives; defaults to an empty object.</summary>
public sealed record EnqueueJobRequest(string Type, string? Payload);
