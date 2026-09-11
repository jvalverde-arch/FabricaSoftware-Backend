using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Domain.Platform;

/// <summary>Users of the current tenant, plus the one cross-tenant lookup that sign-in needs before a tenant is known.</summary>
public interface IAppUserRepository
{
    /// <summary>
    /// Users whose email matches, across tenants: the only query allowed without a session tenant. Sign-in resolves the
    /// tenant from the single match; several matches are an ambiguity the caller must reject.
    /// </summary>
    Task<IReadOnlyList<AppUser>> FindLoginCandidatesAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<AppUser?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Role>> GetRolesAsync(Guid userId, CancellationToken cancellationToken);
}
