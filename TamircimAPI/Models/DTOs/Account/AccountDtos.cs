namespace TamircimAPI.Models.DTOs.Account
{
    // Hesap (tenant) silme talebi — kimlik teyidi için mevcut şifre.
    public class AccountDeletionRequestDTO
    {
        public string Password { get; set; } = string.Empty;
    }

    // Silme planlandı — istemciye kalıcı silme tarihini bildirir.
    public class AccountDeletionResponseDTO
    {
        public DateTime ScheduledAt { get; set; }
    }
}
