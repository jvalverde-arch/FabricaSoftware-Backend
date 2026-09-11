using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Application.Platform.Contracts;

public sealed record SessionUser(Guid Id, Guid TenantId, string Email, string DisplayName, IReadOnlyCollection<Role> Roles);
