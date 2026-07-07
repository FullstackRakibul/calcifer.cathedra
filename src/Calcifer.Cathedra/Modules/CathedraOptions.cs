using System.Reflection;

namespace Calcifer.Cathedra.Modules;

/// <summary>
/// Configures how the kernel discovers modules. The host can point the loader at specific
/// assemblies and/or register module instances directly. If no assemblies are supplied, the loader
/// falls back to scanning the assemblies of the explicitly added modules plus the entry assembly.
/// </summary>
public sealed class CathedraOptions
{
    internal List<Assembly> ModuleAssemblies { get; } = new();
    internal List<IModule> ExplicitModules { get; } = new();


    /// <summary>Scan this assembly for <see cref="IModule"/> implementations.</summary>
    public CathedraOptions AddModuleAssembly(Assembly assembly)
    {
        if (!ModuleAssemblies.Contains(assembly))
            ModuleAssemblies.Add(assembly);
        return this;
    }


    /// <summary>Scan the assembly that contains <typeparamref name="TMarker"/>.</summary>
    public CathedraOptions AddModuleAssemblyContaining<TMarker>() =>
        AddModuleAssembly(typeof(TMarker).Assembly);



    /// <summary>Register a module instance directly, bypassing assembly scanning.</summary>
    public CathedraOptions AddModule(IModule module)
    {
        ExplicitModules.Add(module);
        return this;
    }


    /// <summary>Register a module by type; it is created with its public parameterless constructor.</summary>
    public CathedraOptions AddModule<TModule>() where TModule : IModule, new() =>
        AddModule(new TModule());
}
