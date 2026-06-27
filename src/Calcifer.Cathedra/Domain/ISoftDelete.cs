namespace Calcifer.Cathedra.Domain;

/// <summary>
/// Marks an entity that is soft-deleted: a delete sets <see cref="IsDeleted"/> instead of
/// removing the row. <c>CathedraDbContextBase</c> converts deletes to updates and applies a
/// global query filter so soft-deleted rows are hidden by default.
/// </summary>
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAtUtc { get; set; }
    string? DeletedBy { get; set; }
}
