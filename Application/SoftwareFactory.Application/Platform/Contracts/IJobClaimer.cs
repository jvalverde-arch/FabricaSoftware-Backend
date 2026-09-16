namespace SoftwareFactory.Application.Platform.Contracts;

/// <summary>
/// Takes the next runnable job across tenants with <c>FOR UPDATE SKIP LOCKED</c> (doc 03, D6), so several workers
/// never pick the same one. A run whose lease expired (a worker died) becomes claimable again.
/// </summary>
public interface IJobClaimer
{
    Task<ClaimedJob?> ClaimNextAsync(CancellationToken cancellationToken);
}
