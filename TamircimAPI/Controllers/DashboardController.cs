using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TamircimAPI.Services.Dashboard;

namespace TamircimAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var result = await _dashboardService.GetDashboardAsync();
            return Ok(result);
        }
    }
}
