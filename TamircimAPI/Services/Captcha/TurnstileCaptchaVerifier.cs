using System.Text.Json;

namespace TamircimAPI.Services.Captcha
{
    // Cloudflare Turnstile doğrulaması. İstemcinin aldığı token'ı sunucu tarafında
    // siteverify uç noktasında doğrular (token istemcide üretilir ama GÜVEN sunucudadır).
    //
    // GÜVENLİK: TURNSTILE_SECRET yapılandırılmamışsa fail-closed davranır (doğrulama
    // başarısız sayılır) → yanlış yapılandırmada bot koruması sessizce devre dışı kalmaz.
    // Yerel/test için Cloudflare'in "her zaman geçer" test secret'ı kullanılabilir.
    public class TurnstileCaptchaVerifier : ICaptchaVerifier
    {
        private const string VerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TurnstileCaptchaVerifier> _logger;

        public TurnstileCaptchaVerifier(IHttpClientFactory httpClientFactory, ILogger<TurnstileCaptchaVerifier> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<bool> VerifyAsync(string? token, string? remoteIp)
        {
            var secret = Environment.GetEnvironmentVariable("TURNSTILE_SECRET");
            if (string.IsNullOrWhiteSpace(secret))
            {
                _logger.LogError("TURNSTILE_SECRET yapılandırılmamış — captcha doğrulaması fail-closed (reddedildi).");
                return false;
            }

            if (string.IsNullOrWhiteSpace(token))
                return false;

            try
            {
                var form = new Dictionary<string, string>
                {
                    ["secret"] = secret,
                    ["response"] = token,
                };
                if (!string.IsNullOrWhiteSpace(remoteIp))
                    form["remoteip"] = remoteIp;

                var client = _httpClientFactory.CreateClient(nameof(TurnstileCaptchaVerifier));
                using var resp = await client.PostAsync(VerifyUrl, new FormUrlEncodedContent(form));
                var json = await resp.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                var success = doc.RootElement.TryGetProperty("success", out var s) && s.GetBoolean();
                if (!success)
                    _logger.LogWarning("Turnstile doğrulaması başarısız: {Json}", json);
                return success;
            }
            catch (Exception ex)
            {
                // Ağ/sağlayıcı hatasında fail-closed (güvenlik önceliği).
                _logger.LogError(ex, "Turnstile doğrulaması sırasında hata.");
                return false;
            }
        }
    }
}
