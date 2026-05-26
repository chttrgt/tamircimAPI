using TamircimAPI.Models;

namespace TamircimAPI.Services.Token
{
    public interface ITokenService
    {
        string GenerateAccessToken(User user);
        string GenerateRefreshToken();
    }
}
