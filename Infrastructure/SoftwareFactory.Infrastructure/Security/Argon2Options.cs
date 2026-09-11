namespace SoftwareFactory.Infrastructure.Security;

/// <summary>Argon2id parameters (estandar-auth.md §2: documented in configuration). Defaults follow the OWASP recommendation.</summary>
public sealed class Argon2Options
{
    public const string SectionName = "Argon2";

    public int MemoryKiB { get; set; } = 65536;

    public int Iterations { get; set; } = 3;

    public int Parallelism { get; set; } = 1;

    public int SaltLength { get; set; } = 16;

    public int HashLength { get; set; } = 32;

    public bool IsValid() =>
        MemoryKiB >= 8 * Parallelism
        && Iterations >= 1
        && Parallelism >= 1
        && SaltLength >= 8
        && HashLength >= 16;
}
