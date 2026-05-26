namespace TamircimAPI.Models.Interfaces
{
    public interface ISoftDeletable
    {
        bool IsDeleted { get; set; }
        DateTime? DeletedAt { get; set; }
        int? DeletedByUserId { get; set; }
    }
}
