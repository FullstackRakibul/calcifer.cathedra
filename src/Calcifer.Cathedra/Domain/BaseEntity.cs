namespace Calcifer.Cathedra.Domain;

/// <summary>
/// Base for all persisted entities: a GUID primary key plus the audit and soft-delete columns,
/// so any module's aggregate gets identity, auditing, and soft-delete for free.
/// </summary>
public abstract class BaseEntity : IAuditable, ISoftDelete
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // IAuditable
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }

    // ISoftDelete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
