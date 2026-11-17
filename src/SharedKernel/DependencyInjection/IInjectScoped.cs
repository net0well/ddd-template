namespace SharedKernel.DependencyInjection;

/// <summary>
/// Registers the class automatically as <b>scoped</b> (as itself and its interfaces).
/// Use for anything that depends on the DbContext: repositories, unit of work, domain services.
/// </summary>
public interface IInjectScoped;
