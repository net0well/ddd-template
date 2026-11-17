using SharedKernel.Notifications;

namespace SharedKernel.Primitives;

/// <summary>
/// Base for value objects: no identity, equality by value. Compare with <see cref="Equals(object)"/>, not <c>==</c>.
/// </summary>
public abstract class ValueObject : Notifiable
{
    public override bool Equals(object? obj) =>
        obj is ValueObject other &&
        other.GetType() == GetType() &&
        GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());

    public override int GetHashCode()
    {
        var hash = new HashCode();

        foreach (object? component in GetEqualityComponents())
        {
            hash.Add(component);
        }

        return hash.ToHashCode();
    }

    protected abstract IEnumerable<object?> GetEqualityComponents();
}
