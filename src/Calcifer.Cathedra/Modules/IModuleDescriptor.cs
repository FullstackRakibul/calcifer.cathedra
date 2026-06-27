namespace Calcifer.Cathedra.Modules;

/// <summary>
/// Static metadata about a module, used for discovery logging and ordering. Kept separate from
/// <see cref="IModule"/> so a module's identity can be inspected without instantiating its behavior.
/// </summary>
public interface IModuleDescriptor
{
    /// <summary>Human-readable module name, e.g. "Public".</summary>
    string Name { get; }

    /// <summary>Semantic-ish version string, e.g. "0.1.0".</summary>
    string Version { get; }

    /// <summary>
    /// Load order: lower values are registered and started first. Modules with the same order are
    /// loaded in discovery order. Defaults to 0.
    /// </summary>
    int Order => 0;
}
