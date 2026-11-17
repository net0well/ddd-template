namespace SharedKernel.DependencyInjection;

/// <summary>
/// Registers the class automatically as <b>singleton</b> (as itself and its interfaces).
/// Only for thread-safe services that do not depend on scoped ones.
/// </summary>
public interface IInjectSingleton;
