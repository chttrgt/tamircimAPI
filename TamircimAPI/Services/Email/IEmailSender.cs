namespace TamircimAPI.Services.Email
{
    public interface IEmailSender
    {
        // lang: "tr" | "en" | "de" (bilinmeyen → tr). E-posta içeriği bu dile göre seçilir.
        Task SendVerificationEmailAsync(string toEmail, string toName, string verificationLink, string lang);
    }
}
