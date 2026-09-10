using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SoftwareFactory.Infrastructure.Security;

namespace SoftwareFactory.Infrastructure.Tests.Security;

public sealed partial class Argon2PasswordHasherTests
{
    private const string Password = "Correct-Horse-Battery-Staple";

    [Fact]
    public void Hash_produces_phc_string_with_configured_parameters()
    {
        var hash = CreateHasher(memoryKiB: 8192, iterations: 2, parallelism: 1).Hash(Password);

        Assert.Matches(PhcFormat(), hash);
        Assert.StartsWith("$argon2id$v=19$m=8192,t=2,p=1$", hash, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_accepts_the_password_and_rejects_others()
    {
        var hasher = CreateHasher();
        var hash = hasher.Hash(Password);

        Assert.True(hasher.Verify(hash, Password));
        Assert.False(hasher.Verify(hash, Password + "!"));
        Assert.False(hasher.Verify(hash, "correct-horse-battery-staple"));
    }

    [Fact]
    public void Hashing_the_same_password_twice_uses_different_salts()
    {
        var hasher = CreateHasher();

        Assert.NotEqual(hasher.Hash(Password), hasher.Hash(Password));
    }

    [Fact]
    public void Verify_reads_parameters_from_the_hash_not_from_configuration()
    {
        var hash = CreateHasher(memoryKiB: 8192, iterations: 2, parallelism: 1).Hash(Password);
        var stricterHasher = CreateHasher(memoryKiB: 16384, iterations: 3, parallelism: 2);

        Assert.True(stricterHasher.Verify(hash, Password));
    }

    [Theory]
    [InlineData("plain-text")]
    [InlineData("$argon2i$v=19$m=8192,t=2,p=1$c2FsdHNhbHRzYWx0c2FsdA$aGFzaGhhc2hoYXNoaGFzaGhhc2hoYXNoaGFzaGhhc2g")]
    [InlineData("$argon2id$v=16$m=8192,t=2,p=1$c2FsdHNhbHRzYWx0c2FsdA$aGFzaGhhc2hoYXNoaGFzaGhhc2hoYXNoaGFzaGhhc2g")]
    [InlineData("$argon2id$v=19$m=8192,t=2$c2FsdHNhbHRzYWx0c2FsdA$aGFzaGhhc2hoYXNoaGFzaGhhc2hoYXNoaGFzaGhhc2g")]
    [InlineData("$argon2id$v=19$m=8192,t=2,p=1$not*base64$aGFzaGhhc2hoYXNoaGFzaGhhc2hoYXNoaGFzaGhhc2g")]
    public void Verify_rejects_hashes_that_are_not_argon2id_phc(string malformed) =>
        Assert.False(CreateHasher().Verify(malformed, Password));

    private static Argon2PasswordHasher CreateHasher(int memoryKiB = 8192, int iterations = 2, int parallelism = 1) =>
        new(Options.Create(new Argon2Options { MemoryKiB = memoryKiB, Iterations = iterations, Parallelism = parallelism }));

    // 16-byte salt => 22 unpadded Base64 chars; 32-byte hash => 43 chars.
    [GeneratedRegex(@"^\$argon2id\$v=19\$m=\d+,t=\d+,p=\d+\$[A-Za-z0-9+/]{22}\$[A-Za-z0-9+/]{43}$")]
    private static partial Regex PhcFormat();
}
