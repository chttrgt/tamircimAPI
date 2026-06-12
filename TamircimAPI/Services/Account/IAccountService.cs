namespace TamircimAPI.Services.Account
{
    public interface IAccountService
    {
        // Hesap (tenant) silme talebi: şifre teyidiyle grace süresi başlatır, hesabı askıya alır.
        // Geçen süre dolunca arka plan job'ı kalıcı siler. Kalıcı silme tarihini döner.
        Task<DateTime> RequestDeletionAsync(int userId, string password);

        // Grace süresi içindeyken silmeyi iptal eder — hesap normale döner.
        Task CancelDeletionAsync(int userId);
    }
}
