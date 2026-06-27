namespace Calcifer.Cathedra.Modules;

/// <summary>
/// Immutable, DI-singleton implementation of <see cref="IModuleRegistry"/>. The
/// <see cref="ModuleLoader"/> builds it once during <c>AddCathedra</c>, after discovery and ordering.
/// </summary>
public sealed class ModuleRegistry : IModuleRegistry
{
    public IReadOnlyList<IModule> Modules { get; }

    public ModuleRegistry(IEnumerable<IModule> modules)
    {
        Modules = modules
            .OrderBy(m => m.Order)
            .ToList();
    }
}
