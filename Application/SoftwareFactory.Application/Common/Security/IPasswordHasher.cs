namespace SoftwareFactory.Application.Common.Security;

/// <summary>Password hashing contract. The implementation is Argon2id producing PHC strings (estandar-auth.md §2).</summary>
public interface IPasswordHasher
{
    /// <summary>Hashes a password with a fresh random salt; returns a PHC-formatted string.</summary>
    string Hash(string password);

    /// <summary>Verifies a password against a PHC-formatted hash, using the parameters embedded in the hash.</summary>
    bool Verify(string passwordHash, string password);
}
