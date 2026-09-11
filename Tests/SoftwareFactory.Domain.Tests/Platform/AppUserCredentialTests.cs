using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Domain.Tests.Platform;

/// <summary>Credential changes rotate the security stamp (sessions die); a transparent rehash keeps it.</summary>
public sealed class AppUserCredentialTests
{
    [Fact]
    public void Changing_the_password_hash_rotates_the_security_stamp()
    {
        var user = NewUser();
        var stamp = user.SecurityStamp;

        user.ChangePasswordHash("$argon2id$v=19$m=8,t=1,p=1$c2FsdA$bmV3");

        Assert.Equal("$argon2id$v=19$m=8,t=1,p=1$c2FsdA$bmV3", user.PasswordHash);
        Assert.NotEqual(stamp, user.SecurityStamp);
    }

    [Fact]
    public void Rehashing_with_new_parameters_keeps_the_security_stamp()
    {
        var user = NewUser();
        var stamp = user.SecurityStamp;

        user.RehashPassword("$argon2id$v=19$m=65536,t=3,p=1$c2FsdA$bmV3");

        Assert.Equal("$argon2id$v=19$m=65536,t=3,p=1$c2FsdA$bmV3", user.PasswordHash);
        Assert.Equal(stamp, user.SecurityStamp);
    }

    [Fact]
    public void Security_stamp_can_be_rotated_on_its_own()
    {
        var user = NewUser();
        var stamp = user.SecurityStamp;

        user.RotateSecurityStamp();

        Assert.NotEqual(stamp, user.SecurityStamp);
        Assert.Equal(32, user.SecurityStamp.Length);
    }

    private static AppUser NewUser() => new(Guid.CreateVersion7(), "user@tenant.test", "User", "$argon2id$v=19$m=8,t=1,p=1$c2FsdA$aGFzaA");
}
