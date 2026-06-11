using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace TamircimAPI.Services.Email
{
    // SMTP üzerinden e-posta gönderimi (MailKit). Ayarlar ortam değişkenlerinden:
    //   SMTP_HOST, SMTP_PORT, SMTP_USER, SMTP_PASS, SMTP_FROM, SMTP_FROM_NAME
    // Eksik yapılandırmada açılış anında değil, gönderim anında net hata verir.
    public class SmtpEmailSender : IEmailSender
    {
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(ILogger<SmtpEmailSender> logger)
        {
            _logger = logger;
        }

        public async Task SendVerificationEmailAsync(string toEmail, string toName, string verificationLink, string lang)
        {
            var host = Require("SMTP_HOST");
            var port = int.TryParse(Environment.GetEnvironmentVariable("SMTP_PORT"), out var p) ? p : 587;
            var user = Environment.GetEnvironmentVariable("SMTP_USER");
            var pass = Environment.GetEnvironmentVariable("SMTP_PASS");
            var from = Require("SMTP_FROM");
            var fromName = Environment.GetEnvironmentVariable("SMTP_FROM_NAME") ?? "Tamircim";

            var s = VerificationStrings(lang);

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, from));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = s.Subject;

            var builder = new BodyBuilder
            {
                HtmlBody = BuildHtmlBody(toName, verificationLink, from, s),
                TextBody = BuildTextBody(toName, verificationLink, s),
            };
            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            // STARTTLS (587) yaygın; 465 için SslOnConnect. Porta göre otomatik seç.
            var socketOptions = port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            await client.ConnectAsync(host, port, socketOptions);
            if (!string.IsNullOrEmpty(user))
                await client.AuthenticateAsync(user, pass ?? string.Empty);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Doğrulama e-postası gönderildi: {Email} ({Lang})", toEmail, lang);
        }

        private static string Require(string key) =>
            Environment.GetEnvironmentVariable(key)
            ?? throw new InvalidOperationException($"{key} ortam değişkeni yapılandırılmamış (e-posta gönderimi için zorunlu).");

        // Lokalize metin parçaları. {0} ile gönderici adresini (spam notu) tutar.
        private sealed record Strings(
            string Subject, string Greeting, string Intro, string Button,
            string LinkFallback, string Expiry, string SpamNote, string Footer);

        private static Strings VerificationStrings(string lang) => lang switch
        {
            "en" => new Strings(
                "Verify your Tamircim account",
                "Hello {0},",
                "Thanks for creating your Tamircim account. Click the button below to activate it:",
                "Verify My Email",
                "If the button doesn't work, paste this link into your browser:",
                "This link is valid for 24 hours. If you didn't create this account, you can ignore this email.",
                "📩 If you found this email in your <strong>Spam/Junk</strong> folder, please add <strong>{0}</strong> to your contacts or mark it as \"Not spam\" so future messages reach your inbox.",
                "© Tamircim — Repair Service Management"),
            "de" => new Strings(
                "Bestätigen Sie Ihr Tamircim-Konto",
                "Hallo {0},",
                "Danke, dass Sie Ihr Tamircim-Konto erstellt haben. Klicken Sie auf die Schaltfläche unten, um es zu aktivieren:",
                "E-Mail bestätigen",
                "Falls die Schaltfläche nicht funktioniert, fügen Sie diesen Link in Ihren Browser ein:",
                "Dieser Link ist 24 Stunden gültig. Falls Sie dieses Konto nicht erstellt haben, ignorieren Sie diese E-Mail.",
                "📩 Falls diese E-Mail in Ihrem <strong>Spam-/Junk-Ordner</strong> gelandet ist, fügen Sie bitte <strong>{0}</strong> zu Ihren Kontakten hinzu oder markieren Sie sie als \"Kein Spam\", damit künftige Nachrichten Ihren Posteingang erreichen.",
                "© Tamircim — Verwaltung technischer Dienste"),
            _ => new Strings(
                "Tamircim hesabını doğrula",
                "Merhaba {0},",
                "Tamircim hesabını oluşturduğun için teşekkürler. Hesabını etkinleştirmek için aşağıdaki butona tıkla:",
                "E-postamı Doğrula",
                "Buton çalışmazsa bu bağlantıyı tarayıcına yapıştır:",
                "Bu bağlantı 24 saat geçerlidir. Bu kaydı sen yapmadıysan bu e-postayı yok sayabilirsin.",
                "📩 Bu e-postayı <strong>Spam/Önemsiz</strong> klasöründe bulduysan, gelecekte mesajlarımızın gelen kutuna düşmesi için lütfen <strong>{0}</strong> adresini kişilerine ekle veya \"Spam değil\" olarak işaretle.",
                "© Tamircim — Teknik Servis Yönetimi"),
        };

        // E-posta istemcileri <style> bloklarını ve modern CSS'i sık sık atar; bu yüzden
        // tablo tabanlı düzen + INLINE stiller kullanılır (Gmail/Outlook/Apple Mail uyumu).
        private static string BuildHtmlBody(string name, string link, string fromAddress, Strings s)
        {
            var safeName = System.Net.WebUtility.HtmlEncode(name);
            var greeting = string.Format(s.Greeting, safeName);
            var spamNote = string.Format(s.SpamNote, System.Net.WebUtility.HtmlEncode(fromAddress));
            return $@"<!DOCTYPE html>
<html>
<head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background-color:#f1f5f9;"">
  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f1f5f9;padding:24px 0;"">
    <tr><td align=""center"">
      <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""max-width:480px;background-color:#ffffff;border-radius:14px;overflow:hidden;box-shadow:0 2px 8px rgba(15,23,42,0.06);"">
        <tr><td style=""background-color:#06B6D4;padding:28px 32px;text-align:center;"">
          <span style=""font-family:Arial,Helvetica,sans-serif;font-size:24px;font-weight:bold;color:#ffffff;letter-spacing:1px;"">Tamircim</span>
        </td></tr>
        <tr><td style=""padding:32px;font-family:Arial,Helvetica,sans-serif;color:#0f172a;"">
          <p style=""margin:0 0 12px;font-size:16px;"">{greeting}</p>
          <p style=""margin:0 0 24px;font-size:15px;line-height:22px;color:#334155;"">{s.Intro}</p>
          <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""margin:0 auto 24px;"">
            <tr><td align=""center"" style=""border-radius:10px;background-color:#06B6D4;"">
              <a href=""{link}"" target=""_blank""
                 style=""display:inline-block;padding:14px 32px;font-family:Arial,Helvetica,sans-serif;font-size:16px;font-weight:bold;color:#ffffff;text-decoration:none;border-radius:10px;"">{s.Button}</a>
            </td></tr>
          </table>
          <p style=""margin:0 0 8px;font-size:13px;color:#64748b;"">{s.LinkFallback}</p>
          <p style=""margin:0 0 24px;font-size:12px;word-break:break-all;"">
            <a href=""{link}"" target=""_blank"" style=""color:#06B6D4;"">{link}</a>
          </p>
          <p style=""margin:0 0 16px;font-size:13px;color:#64748b;"">{s.Expiry}</p>
          <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f8fafc;border-radius:10px;border:1px solid #e2e8f0;"">
            <tr><td style=""padding:14px 16px;font-family:Arial,Helvetica,sans-serif;font-size:12px;line-height:18px;color:#64748b;"">{spamNote}</td></tr>
          </table>
        </td></tr>
        <tr><td style=""padding:20px 32px;background-color:#f8fafc;border-top:1px solid #e2e8f0;text-align:center;"">
          <span style=""font-family:Arial,Helvetica,sans-serif;font-size:11px;color:#94a3b8;"">{s.Footer}</span>
        </td></tr>
      </table>
    </td></tr>
  </table>
</body>
</html>";
        }

        private static string BuildTextBody(string name, string link, Strings s)
        {
            // Düz metinde HTML etiketlerini temizle (spam notundaki <strong>).
            var spam = s.SpamNote.Replace("<strong>", "").Replace("</strong>", "");
            return $@"{string.Format(s.Greeting, name)}

{s.Intro}

{link}

{s.Expiry}

{string.Format(spam, GetFromAddressText())}

— Tamircim";
        }

        private static string GetFromAddressText() =>
            Environment.GetEnvironmentVariable("SMTP_FROM") ?? "Tamircim";

        // ── Şifre sıfırlama (6 haneli kod) ────────────────────────────────────────
        public async Task SendPasswordResetEmailAsync(string toEmail, string toName, string code, string lang)
        {
            var host = Require("SMTP_HOST");
            var port = int.TryParse(Environment.GetEnvironmentVariable("SMTP_PORT"), out var p) ? p : 587;
            var user = Environment.GetEnvironmentVariable("SMTP_USER");
            var pass = Environment.GetEnvironmentVariable("SMTP_PASS");
            var from = Require("SMTP_FROM");
            var fromName = Environment.GetEnvironmentVariable("SMTP_FROM_NAME") ?? "Tamircim";

            var s = ResetStrings(lang);

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, from));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = s.Subject;

            var builder = new BodyBuilder
            {
                HtmlBody = BuildResetHtmlBody(toName, code, from, s),
                TextBody = BuildResetTextBody(toName, code, s),
            };
            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            var socketOptions = port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            await client.ConnectAsync(host, port, socketOptions);
            if (!string.IsNullOrEmpty(user))
                await client.AuthenticateAsync(user, pass ?? string.Empty);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Şifre sıfırlama e-postası gönderildi: {Email} ({Lang})", toEmail, lang);
        }

        // ── İki adımlı doğrulama (6 haneli kod) ───────────────────────────────────
        // Şifre sıfırlama e-postasının HTML/metin gövdesini yeniden kullanır; yalnızca
        // metinler (konu/açıklama/süre) 2FA'ya özeldir.
        public async Task SendTwoFactorCodeEmailAsync(string toEmail, string toName, string code, string lang)
        {
            var host = Require("SMTP_HOST");
            var port = int.TryParse(Environment.GetEnvironmentVariable("SMTP_PORT"), out var p) ? p : 587;
            var user = Environment.GetEnvironmentVariable("SMTP_USER");
            var pass = Environment.GetEnvironmentVariable("SMTP_PASS");
            var from = Require("SMTP_FROM");
            var fromName = Environment.GetEnvironmentVariable("SMTP_FROM_NAME") ?? "Tamircim";

            var s = TwoFactorStrings(lang);

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, from));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = s.Subject;

            var builder = new BodyBuilder
            {
                HtmlBody = BuildResetHtmlBody(toName, code, from, s),
                TextBody = BuildResetTextBody(toName, code, s),
            };
            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            var socketOptions = port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            await client.ConnectAsync(host, port, socketOptions);
            if (!string.IsNullOrEmpty(user))
                await client.AuthenticateAsync(user, pass ?? string.Empty);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("2FA kodu e-postası gönderildi: {Email} ({Lang})", toEmail, lang);
        }

        private static ResetStrings_ TwoFactorStrings(string lang) => lang switch
        {
            "en" => new ResetStrings_(
                "Your Tamircim verification code",
                "Hello {0},",
                "Use the code below to complete your sign-in:",
                "Verification code",
                "This code is valid for 5 minutes.",
                "If you didn't try to sign in, ignore this email and change your password.",
                "📩 If this email landed in your <strong>Spam/Junk</strong> folder, please add <strong>{0}</strong> to your contacts.",
                "© Tamircim — Repair Service Management"),
            "de" => new ResetStrings_(
                "Ihr Tamircim-Verifizierungscode",
                "Hallo {0},",
                "Verwenden Sie den folgenden Code, um die Anmeldung abzuschließen:",
                "Code",
                "Dieser Code ist 5 Minuten gültig.",
                "Falls Sie sich nicht anmelden wollten, ignorieren Sie diese E-Mail und ändern Sie Ihr Passwort.",
                "📩 Falls diese E-Mail im <strong>Spam-/Junk-Ordner</strong> gelandet ist, fügen Sie bitte <strong>{0}</strong> zu Ihren Kontakten hinzu.",
                "© Tamircim — Verwaltung technischer Dienste"),
            _ => new ResetStrings_(
                "Tamircim doğrulama kodun",
                "Merhaba {0},",
                "Girişini tamamlamak için aşağıdaki kodu kullan:",
                "Doğrulama kodu",
                "Bu kod 5 dakika geçerlidir.",
                "Bu girişi sen yapmadıysan bu e-postayı yok say ve şifreni değiştir.",
                "📩 Bu e-postayı <strong>Spam/Önemsiz</strong> klasöründe bulduysan lütfen <strong>{0}</strong> adresini kişilerine ekle.",
                "© Tamircim — Teknik Servis Yönetimi"),
        };

        private sealed record ResetStrings_(
            string Subject, string Greeting, string Intro, string CodeLabel,
            string Expiry, string Ignore, string SpamNote, string Footer);

        private static ResetStrings_ ResetStrings(string lang) => lang switch
        {
            "en" => new ResetStrings_(
                "Your Tamircim password reset code",
                "Hello {0},",
                "Use the code below to reset your Tamircim password:",
                "Reset code",
                "This code is valid for 15 minutes.",
                "If you didn't request a password reset, you can ignore this email — your password stays the same.",
                "📩 If this email landed in your <strong>Spam/Junk</strong> folder, please add <strong>{0}</strong> to your contacts.",
                "© Tamircim — Repair Service Management"),
            "de" => new ResetStrings_(
                "Ihr Tamircim-Code zum Zurücksetzen des Passworts",
                "Hallo {0},",
                "Verwenden Sie den folgenden Code, um Ihr Tamircim-Passwort zurückzusetzen:",
                "Code",
                "Dieser Code ist 15 Minuten gültig.",
                "Falls Sie kein Zurücksetzen angefordert haben, ignorieren Sie diese E-Mail — Ihr Passwort bleibt gleich.",
                "📩 Falls diese E-Mail im <strong>Spam-/Junk-Ordner</strong> gelandet ist, fügen Sie bitte <strong>{0}</strong> zu Ihren Kontakten hinzu.",
                "© Tamircim — Verwaltung technischer Dienste"),
            _ => new ResetStrings_(
                "Tamircim şifre sıfırlama kodun",
                "Merhaba {0},",
                "Tamircim şifreni sıfırlamak için aşağıdaki kodu kullan:",
                "Sıfırlama kodu",
                "Bu kod 15 dakika geçerlidir.",
                "Şifre sıfırlama talebinde bulunmadıysan bu e-postayı yok sayabilirsin — şifren aynı kalır.",
                "📩 Bu e-postayı <strong>Spam/Önemsiz</strong> klasöründe bulduysan lütfen <strong>{0}</strong> adresini kişilerine ekle.",
                "© Tamircim — Teknik Servis Yönetimi"),
        };

        private static string BuildResetHtmlBody(string name, string code, string fromAddress, ResetStrings_ s)
        {
            var safeName = System.Net.WebUtility.HtmlEncode(name);
            var greeting = string.Format(s.Greeting, safeName);
            var spamNote = string.Format(s.SpamNote, System.Net.WebUtility.HtmlEncode(fromAddress));
            return $@"<!DOCTYPE html>
<html>
<head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background-color:#f1f5f9;"">
  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f1f5f9;padding:24px 0;"">
    <tr><td align=""center"">
      <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""max-width:480px;background-color:#ffffff;border-radius:14px;overflow:hidden;box-shadow:0 2px 8px rgba(15,23,42,0.06);"">
        <tr><td style=""background-color:#06B6D4;padding:28px 32px;text-align:center;"">
          <span style=""font-family:Arial,Helvetica,sans-serif;font-size:24px;font-weight:bold;color:#ffffff;letter-spacing:1px;"">Tamircim</span>
        </td></tr>
        <tr><td style=""padding:32px;font-family:Arial,Helvetica,sans-serif;color:#0f172a;"">
          <p style=""margin:0 0 12px;font-size:16px;"">{greeting}</p>
          <p style=""margin:0 0 20px;font-size:15px;line-height:22px;color:#334155;"">{s.Intro}</p>
          <p style=""margin:0 0 6px;font-size:12px;color:#64748b;text-transform:uppercase;letter-spacing:1px;"">{s.CodeLabel}</p>
          <div style=""margin:0 0 20px;padding:18px;text-align:center;background-color:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;font-family:'Courier New',monospace;font-size:34px;font-weight:bold;letter-spacing:8px;color:#0f172a;"">{code}</div>
          <p style=""margin:0 0 16px;font-size:13px;color:#64748b;"">{s.Expiry}</p>
          <p style=""margin:0 0 20px;font-size:13px;color:#64748b;"">{s.Ignore}</p>
          <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f8fafc;border-radius:10px;border:1px solid #e2e8f0;"">
            <tr><td style=""padding:14px 16px;font-family:Arial,Helvetica,sans-serif;font-size:12px;line-height:18px;color:#64748b;"">{spamNote}</td></tr>
          </table>
        </td></tr>
        <tr><td style=""padding:20px 32px;background-color:#f8fafc;border-top:1px solid #e2e8f0;text-align:center;"">
          <span style=""font-family:Arial,Helvetica,sans-serif;font-size:11px;color:#94a3b8;"">{s.Footer}</span>
        </td></tr>
      </table>
    </td></tr>
  </table>
</body>
</html>";
        }

        private static string BuildResetTextBody(string name, string code, ResetStrings_ s)
        {
            var spam = s.SpamNote.Replace("<strong>", "").Replace("</strong>", "");
            return $@"{string.Format(s.Greeting, name)}

{s.Intro}

{s.CodeLabel}: {code}

{s.Expiry}
{s.Ignore}

{string.Format(spam, GetFromAddressText())}

— Tamircim";
        }
    }
}
