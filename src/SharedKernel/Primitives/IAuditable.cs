namespace SharedKernel.Primitives;

/// <summary>
/// Opts an entity into automatic auditing: who created it and when, who changed it last and when, stamped on
/// every save. Implement the properties with private setters. Bulk <c>ExecuteUpdate</c>/<c>ExecuteDelete</c>
/// bypass it.
/// </summary>
public interface IAuditable
{
    DateTime CreatedOnUtc { get; }

    /// <summary>The authenticated user, or <see langword="null"/> for anonymous requests and background work.</summary>
    Guid? CreatedBy { get; }

    DateTime? ModifiedOnUtc { get; }

    Guid? ModifiedBy { get; }
}
