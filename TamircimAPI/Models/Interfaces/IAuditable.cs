namespace TamircimAPI.Models.Interfaces
{
    public interface IAuditable
    {
        DateTime CreatedAt { get; set; }
        DateTime UpdatedAt { get; set; }
        int? CreatedByUserId { get; set; }
        int? UpdatedByUserId { get; set; }
    }
}
