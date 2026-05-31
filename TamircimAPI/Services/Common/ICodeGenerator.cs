namespace TamircimAPI.Services.Common
{
    // Benzersiz, okunabilir kodlar üretir (DB sequence tabanlı, yarış koşulsuz).
    public interface ICodeGenerator
    {
        Task<string> NextDeviceCodeAsync();
        Task<string> NextTicketNoAsync();
    }
}
