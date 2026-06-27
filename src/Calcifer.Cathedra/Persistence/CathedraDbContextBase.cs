using Calcifer.Cathedra.Domain;
using Microsoft.EntityFrameworkCore;

namespace Calcifer.Cathedra.Persistence;

/// <summary>
/// Base <see cref="DbContext"/> that enforces the kernel's persistence conventions so every module
/// gets them for free:
/// <list type="bullet">
///   <item>audit columns (<see cref="IAuditable"/>) stamped on insert/update,</item>
///   <item>soft delete (<see cref="ISoftDelete"/>): deletes become updates,</item>
///   <item>a global query filter that hides soft-deleted rows.</item>
/// </list>
/// Modules derive their own context from this (or use <c>CathedraDbContext</c>) and add their
/// <see cref="DbSet{T}"/>s.
/// </summary>
public abstract class CathedraDbContextBase : DbContext
{
    private readonly ICurrentUser _currentUser;

    protected CathedraDbContextBase(DbContextOptions options, ICurrentUser currentUser)
        : base(options)
    {
        _currentUser = currentUser;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply a soft-delete query filter to every entity that implements ISoftDelete.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(BuildIsNotDeletedFilter(entityType.ClrType));
            }
        }
    }

    public override int SaveChanges()
    {
        ApplyAuditAndSoftDelete();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditAndSoftDelete();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditAndSoftDelete()
    {
        var nowUtc = DateTime.UtcNow;
        var user = _currentUser.UserId ?? _currentUser.UserName;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is IAuditable auditable)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        auditable.CreatedAtUtc = nowUtc;
                        auditable.CreatedBy = user;
                        break;
                    case EntityState.Modified:
                        auditable.UpdatedAtUtc = nowUtc;
                        auditable.UpdatedBy = user;
                        break;
                }
            }

            // Convert hard deletes into soft deletes.
            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDelete softDelete)
            {
                entry.State = EntityState.Modified;
                softDelete.IsDeleted = true;
                softDelete.DeletedAtUtc = nowUtc;
                softDelete.DeletedBy = user;
            }
        }
    }

    /// <summary>Builds the expression <c>e =&gt; !e.IsDeleted</c> for the given entity type.</summary>
    private static System.Linq.Expressions.LambdaExpression BuildIsNotDeletedFilter(Type entityClrType)
    {
        var parameter = System.Linq.Expressions.Expression.Parameter(entityClrType, "e");
        var property = System.Linq.Expressions.Expression.Property(
            parameter, nameof(ISoftDelete.IsDeleted));
        var notDeleted = System.Linq.Expressions.Expression.Not(property);
        return System.Linq.Expressions.Expression.Lambda(notDeleted, parameter);
    }
}
