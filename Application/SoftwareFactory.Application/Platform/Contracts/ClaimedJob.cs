namespace SoftwareFactory.Application.Platform.Contracts;

/// <summary>A run already marked as running by the claim, handed to the worker to execute.</summary>
public sealed record ClaimedJob(Guid JobId, Guid TenantId, string Type, string Payload, int Attempt);
