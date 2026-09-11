namespace SoftwareFactory.Domain.Common;

/// <summary>Base of every persisted entity: a time-ordered UUID v7 identity generated in the application.</summary>
public abstract class Entity
{
    protected Entity()
    {
        Id = Guid.CreateVersion7();
    }

    /// <summary>For the few entities whose identity is fixed by convention (seed data); everything else generates its own id.</summary>
    protected Entity(Guid id)
    {
        Guard.NotEmpty(id);
        Id = id;
    }

    public Guid Id { get; private set; }
}
