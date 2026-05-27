using TamircimAPI.Models.DTOs.Dashboard;

namespace TamircimAPI.Services.Dashboard
{
    public interface IDashboardService
    {
        Task<DashboardResponseDTO> GetDashboardAsync();
    }
}
