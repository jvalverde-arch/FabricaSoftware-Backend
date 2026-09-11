using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Domain.Tests.Platform;

/// <summary>Append-only audit record (estandar-auth.md §6): who, what, when, from where.</summary>
public sealed class AuditEventTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Records_action_actor_and_client_details()
    {
        var tenantId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();

        var audit = new AuditEvent(tenantId, AuditAction.LoginSucceeded, AuthorType.Human, userId, new AuditClient("10.0.0.1", "Mozilla/5.0"), _now);

        Assert.Equal(tenantId, audit.TenantId);
        Assert.Equal(AuditAction.LoginSucceeded, audit.Action);
        Assert.Equal(AuthorType.Human, audit.ActorType);
        Assert.Equal(userId, audit.ActorId);
        Assert.Equal("10.0.0.1", audit.IpAddress);
        Assert.Equal("Mozilla/5.0", audit.UserAgent);
        Assert.Equal(_now, audit.OccurredAt);
        Assert.Null(audit.Details);
    }

    [Fact]
    public void Details_must_be_valid_json_when_present()
    {
        var tenantId = Guid.CreateVersion7();

        var audit = new AuditEvent(tenantId, AuditAction.RefreshReuseDetected, null, null, new AuditClient(null, null), _now, """{"family":"abc"}""");
        Assert.Equal("""{"family":"abc"}""", audit.Details);

        Assert.Throws<ArgumentException>(() => new AuditEvent(tenantId, AuditAction.Logout, null, null, new AuditClient(null, null), _now, "not json"));
    }

    [Fact]
    public void Client_user_agent_is_truncated_to_a_sane_length()
    {
        var client = new AuditClient("::1", new string('a', 2000));

        Assert.Equal(AuditClient.MaxUserAgentLength, client.UserAgent!.Length);
    }
}
