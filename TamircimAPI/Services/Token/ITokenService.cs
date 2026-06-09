using TamircimAPI.Models;

namespace TamircimAPI.Services.Token
{
    public interface ITokenService
    {
        string GenerateAccessToken(User user);
        string GenerateRefreshToken();

        // Token'ı (refresh / e-posta doğrulama) DB'de saklamadan önce SHA-256 ile
        // hash'ler. Düz metin yalnızca istemciye/e-postaya gider; DB sızsa bile
        // saklanan hash'ten orijinal token elde edilemez.
        string HashToken(string token);
    }
}
