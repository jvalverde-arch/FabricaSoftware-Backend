using System.Text.RegularExpressions;
using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Domain.Platform;

/// <summary>A customer of the platform. Every other table is scoped to one tenant.</summary>
public sealed partial class Tenant : Entity
{
    private Tenant()
    {
    }

    public Tenant(string name, string slug)
        : this(Guid.CreateVersion7(), name, slug)
    {
    }

    public Tenant(Guid id, string name, string slug)
        : base(id)
    {
        Name = Guard.NotBlank(name);
        Slug = ValidateSlug(slug);
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>URL-safe unique identifier: lowercase letters, digits and hyphens.</summary>
    public string Slug { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string ValidateSlug(string slug)
    {
        var trimmed = Guard.NotBlank(slug);

        return SlugPattern().IsMatch(trimmed)
            ? trimmed
            : throw new ArgumentException("The slug must contain only lowercase letters, digits and hyphens.", nameof(slug));
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}
