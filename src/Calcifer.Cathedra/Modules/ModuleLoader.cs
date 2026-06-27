using System.Reflection;

namespace Calcifer.Cathedra.Modules;

/// <summary>
/// Discovers <see cref="IModule"/> implementations from the configured assemblies, combines them
/// with any explicitly registered modules, de-duplicates by concrete type, and orders them. The
/// result feeds <see cref="ModuleRegistry"/>. Discovery is deterministic and side-effect free apart
/// from instantiating each module's parameterless constructor.
/// </summary>
public static class ModuleLoader
{
    /// <summary>
    /// Resolve the ordered list of module instances for the given options. Throws if a discovered
    /// module type lacks a public parameterless constructor.
    /// </summary>
    public static IReadOnlyList<IModule> Discover(CathedraOptions options)
    {
        // Where to scan: explicit assemblies, else the assemblies of explicit modules + entry asm.
        var assemblies = new HashSet<Assembly>(options.ModuleAssemblies);
        if (assemblies.Count == 0)
        {
            foreach (var m in options.ExplicitModules)
                assemblies.Add(m.GetType().Assembly);

            var entry = Assembly.GetEntryAssembly();
            if (entry is not null)
                assemblies.Add(entry);
        }

        var byType = new Dictionary<Type, IModule>();

        // Explicitly registered instances win and are never re-instantiated.
        foreach (var module in options.ExplicitModules)
            byType[module.GetType()] = module;

        foreach (var assembly in assemblies)
        {
            foreach (var type in SafeGetTypes(assembly))
            {
                if (!IsConcreteModule(type) || byType.ContainsKey(type))
                    continue;

                var ctor = type.GetConstructor(Type.EmptyTypes)
                    ?? throw new InvalidOperationException(
                        $"Module '{type.FullName}' must have a public parameterless constructor to be discovered.");

                byType[type] = (IModule)ctor.Invoke(null);
            }
        }

        return byType.Values
            .OrderBy(m => m.Order)
            .ThenBy(m => m.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsConcreteModule(Type type) =>
        typeof(IModule).IsAssignableFrom(type) && type is { IsAbstract: false, IsInterface: false };

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Skip types that failed to load (missing optional deps) rather than crash discovery.
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
