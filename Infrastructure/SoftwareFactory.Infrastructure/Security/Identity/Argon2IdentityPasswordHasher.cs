using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SoftwareFactory.Application.Common.Security;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Infrastructure.Security.Identity;

/// <summary>
/// Bridges Identity to the platform's Argon2id hasher. A hash produced with parameters other than the configured ones
/// verifies but asks for a re-hash, so tuning the parameters migrates passwords transparently on the next sign-in.
/// </summary>
internal sealed class Argon2IdentityPasswordHasher(IPasswordHasher hasher, IOptions<Argon2Options> options) : IPasswordHasher<AppUser>
{
    public string HashPassword(AppUser user, string password) => hasher.Hash(password);

    public PasswordVerificationResult VerifyHashedPassword(AppUser user, string hashedPassword, string providedPassword)
    {
        if (!hasher.Verify(hashedPassword, providedPassword))
        {
            return PasswordVerificationResult.Failed;
        }

        var configured = options.Value;

        return Argon2PasswordHasher.TryParse(hashedPassword, out var parsed)
            && parsed.MemoryKiB == configured.MemoryKiB
            && parsed.Iterations == configured.Iterations
            && parsed.Parallelism == configured.Parallelism
                ? PasswordVerificationResult.Success
                : PasswordVerificationResult.SuccessRehashNeeded;
    }
}
