namespace SharedKernel.Persistence;

/// <summary>
/// Marker for the write side of an aggregate (<c>I{Aggregates}WritableRepository</c>): loads aggregates tracked for change,
/// adds and removes them. Never saves — that is the unit of work's job.
/// </summary>
public interface IWritableRepository;
