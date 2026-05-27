using TamircimAPI.Models.DTOs.Auth;

namespace TamircimAPI.Services.Auth
{
    public interface IAuthService
    {
        Task<LoginResponseDTO> LoginAsync(LoginDTO dto, string ipAddress);
        Task<LoginResponseDTO> RegisterAsync(RegisterDTO dto, string ipAddress);
        Task<LoginResponseDTO> RefreshTokenAsync(string refreshToken, string ipAddress);
        Task RevokeTokenAsync(string refreshToken, string ipAddress);
    }
}
