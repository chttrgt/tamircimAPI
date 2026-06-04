using TamircimAPI.Models.DTOs.Auth;

namespace TamircimAPI.Services.Auth
{
    public interface IAuthService
    {
        // Sistemde en az bir kullanıcı (sahip) kurulu mu? İlk-kurulum ekranı kararı için.
        Task<bool> IsInitializedAsync();
        Task<LoginResponseDTO> LoginAsync(LoginDTO dto, string ipAddress);
        Task<LoginResponseDTO> RegisterAsync(RegisterDTO dto, string ipAddress);
        Task<LoginResponseDTO> RefreshTokenAsync(string refreshToken, string ipAddress);
        Task RevokeTokenAsync(string refreshToken, string ipAddress);
        Task<UpdateProfileResponseDTO> UpdateProfileAsync(int userId, UpdateProfileDTO dto, string ipAddress);
    }
}
