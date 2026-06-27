namespace Calcifer.Cathedra.Modules;

/// <summary>
/// The ordered set of modules the kernel discovered and registered. Resolved from DI at pipeline
/// build time so the bootstrapper can map endpoints and run lifecycle hooks in load order.
/// </summary>
public interface IModuleRegistry
{
    /// <summary>Modules in load order (by <see cref="IModuleDescriptor.Order"/>, then discovery order).</summary>
    IReadOnlyList<IModule> Modules { get; }
}
