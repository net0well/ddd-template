namespace SharedKernel.Persistence;

/// <summary>
/// Marker for the read side of an aggregate (<c>I{Aggregates}SearchableRepository</c>): queries only, untracked, never writes.
/// </summary>
public interface ISearchableRepository;
