namespace TamircimAPI.Services.Email
{
    public interface IEmailSender
    {
        // lang: "tr" | "en" | "de" (bilinmeyen → tr). E-posta içeriği bu dile göre seçilir.
        Task SendVerificationEmailAsync(string toEmail, string toName, string verificationLink, string lang);

        // Şifre sıfırlama: kullanıcıya 6 haneli kod gönderir (15 dk geçerli).
        Task SendPasswordResetEmailAsync(string toEmail, string toName, string code, string lang);
    }
}
