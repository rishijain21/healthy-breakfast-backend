namespace Sovva.Domain.Interfaces
{
    /// <summary>
    /// Marks an entity as supporting soft deletes.
    /// When DeletedAt is null the row is active.
    /// When DeletedAt has a value the row is soft-deleted and hidden from all
    /// normal queries via EF Core Global Query Filters.
    /// The TimestampInterceptor automatically sets this value whenever EF tries
    /// to hard-delete an ISoftDeletable entity.
    /// </summary>
    public interface ISoftDeletable
    {
        DateTime? DeletedAt { get; set; }
    }
}
