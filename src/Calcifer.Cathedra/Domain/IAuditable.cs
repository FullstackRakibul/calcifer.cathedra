namespace Calcifer.Cathedra.Domain;

/// <summary>
/// Marks an entity whose create/update audit columns are stamped automatically by the
/// <c>CathedraDbContextBase</c> on <c>SaveChanges</c>.
/// </summary>
public interface IAuditable
{
    DateTime CreatedAtUtc { get; set; }
    string? CreatedBy { get; set; }
    DateTime? UpdatedAtUtc { get; set; }
    string? UpdatedBy { get; set; }
}
