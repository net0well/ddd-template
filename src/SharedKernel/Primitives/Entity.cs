using SharedKernel.Notifications;

namespace SharedKernel.Primitives;

/// <summary>
/// Base for domain objects with identity. The id is a UUID v7 generated on creation
/// (time-ordered: index-friendly and known before saving).
/// </summary>
public abstract class Entity : Notifiable
{
    // Also used by EF Core, which then overwrites Id with the stored value.
    protected Entity()
        : this(Guid.CreateVersion7())
    {
    }

    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("An entity id cannot be empty.", nameof(id));
        }

        Id = id;
    }

    public Guid Id { get; private init; }
}
