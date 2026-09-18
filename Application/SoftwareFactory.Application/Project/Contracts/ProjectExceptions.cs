namespace SoftwareFactory.Application.Project.Contracts;

/// <summary>The project is not in this tenant's list. Nothing is compared to reach this: row-level security
/// already decided that the row does not exist for this session (HU-007 §3).</summary>
public sealed class ProjectNotFoundException : Exception
{
    public ProjectNotFoundException(Guid projectId)
        : base($"Project {projectId} was not found.")
    {
        ProjectId = projectId;
    }

    public ProjectNotFoundException()
    {
    }

    public ProjectNotFoundException(string message)
        : base(message)
    {
    }

    public ProjectNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public Guid ProjectId { get; }
}

/// <summary>
/// The tenant already has a project with that name (HU-007 §4). It travels with the name so the answer can say
/// which one, instead of leaving the person to guess.
/// </summary>
public sealed class ProjectNameTakenException : Exception
{
    public ProjectNameTakenException(string name)
        : base($"A project named '{name}' already exists in this tenant.")
    {
        Name = name;
    }

    public ProjectNameTakenException(string name, Exception innerException)
        : base($"A project named '{name}' already exists in this tenant.", innerException)
    {
        Name = name;
    }

    public ProjectNameTakenException()
    {
    }

    public string Name { get; } = string.Empty;
}

/// <summary>A field of the request does not hold up; it travels as 422 with the field named.</summary>
public sealed class ProjectValidationException : Exception
{
    public ProjectValidationException(string field, string message)
        : base(message)
    {
        Field = field;
    }

    public ProjectValidationException()
    {
    }

    public ProjectValidationException(string message)
        : base(message)
    {
    }

    public ProjectValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public string Field { get; } = string.Empty;
}
