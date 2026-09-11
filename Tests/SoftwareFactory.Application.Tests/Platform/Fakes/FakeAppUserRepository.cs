using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Application.Tests.Platform.Fakes;

internal sealed class FakeAppUserRepository : IAppUserRepository
{
    public List<AppUser> Users { get; } = [];

    public Dictionary<Guid, List<Role>> Roles { get; } = [];

    public Task<IReadOnlyList<AppUser>> FindLoginCandidatesAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AppUser>>([.. Users.Where(user => user.NormalizedEmail == normalizedEmail)]);

    public Task<AppUser?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Users.SingleOrDefault(user => user.Id == id));

    public Task<IReadOnlyList<Role>> GetRolesAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Role>>(Roles.TryGetValue(userId, out var roles) ? roles : []);
}
