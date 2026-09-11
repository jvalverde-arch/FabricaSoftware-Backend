namespace SoftwareFactory.Application.Platform.Contracts;

/// <summary>The signed-in user as the frontend sees it; roles by their wire names (see <c>RoleNames</c>).</summary>
public sealed record SessionUser(Guid Id, Guid TenantId, string Email, string DisplayName, IReadOnlyCollection<string> Roles);
