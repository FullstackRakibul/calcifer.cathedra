using Microsoft.EntityFrameworkCore;

namespace Calcifer.Cathedra.Persistence;

/// <summary>
/// The shared application <see cref="DbContext"/>. Modules contribute their entity configurations
/// via <see cref="IEntityTypeConfiguration{TEntity}"/> types in their own assemblies, which are
/// picked up by <see cref="ApplyConfigurationsFromAssemblyMarkers"/>. Until a module registers an
/// entity, this context has no sets — that is expected for the bare core.
/// </summary>
public sealed class CathedraDbContext : CathedraDbContextBase
{
    private static readonly List<System.Reflection.Assembly> ConfigurationAssemblies = new();

    public CathedraDbContext(DbContextOptions<CathedraDbContext> options, ICurrentUser currentUser)
        : base(options, currentUser)
    {
    }

    /// <summary>
    /// Lets modules register the assembly that holds their <see cref="IEntityTypeConfiguration{TEntity}"/>
    /// types before the context is built (typically in the module's <c>ConfigureServices</c>).
    /// </summary>
    public static void RegisterConfigurationAssembly(System.Reflection.Assembly assembly)
    {
        if (!ConfigurationAssemblies.Contains(assembly))
            ConfigurationAssemblies.Add(assembly);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var assembly in ConfigurationAssemblies)
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);

        // base applies the soft-delete query filters after entities are known.
        base.OnModelCreating(modelBuilder);
    }
}
