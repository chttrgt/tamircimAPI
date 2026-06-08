using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TamircimAPI.Authorization;
using TamircimAPI.Models.DTOs.Device;
using TamircimAPI.Services.Device;

namespace TamircimAPI.Controllers
{
    [ApiController]
    [Route("api/devices")]
    [Authorize]
    public class DeviceController : ControllerBase
    {
        private readonly IDeviceQueryService _query;
        private readonly IDeviceCommandService _command;

        public DeviceController(IDeviceQueryService query, IDeviceCommandService command)
        {
            _query = query;
            _command = command;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int? customerId = null, [FromQuery] string? search = null, [FromQuery] string? filter = null)
        {
            var result = await _query.GetAllAsync(customerId, search, filter);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _query.GetByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        // Cihaz koduna göre birebir bul — barkod okutma için.
        [HttpGet("by-code")]
        public async Task<IActionResult> GetByCode([FromQuery] string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return NotFound();
            var result = await _query.GetByCodeAsync(code);
            if (result == null) return NotFound();
            return Ok(result);
        }

        // Bir müşterinin tüm servis geçmişi (tüm cihazlarındaki gelişler).
        [HttpGet("/api/customers/{customerId:int}/history")]
        public async Task<IActionResult> GetCustomerHistory(int customerId)
        {
            var result = await _query.GetCustomerHistoryAsync(customerId);
            return Ok(result);
        }

        // Seri no çakışma kontrolü.
        [HttpGet("check-serial")]
        public async Task<IActionResult> CheckSerial([FromQuery] string serial, [FromQuery] int? excludeDeviceId = null)
        {
            if (string.IsNullOrWhiteSpace(serial))
                return Ok(new SerialCheckResultDTO { Exists = false });
            var result = await _query.CheckSerialAsync(serial, excludeDeviceId);
            return Ok(result);
        }

        [HttpPost]
        [HasPermission(Permissions.DevicesCreate)]
        public async Task<IActionResult> Create([FromBody] CreateDeviceDTO dto)
        {
            var result = await _command.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpPut("{id:int}")]
        [HasPermission(Permissions.DevicesEdit)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateDeviceDTO dto)
        {
            var result = await _command.UpdateAsync(id, dto);
            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        [HasPermission(Permissions.DevicesDelete)]
        public async Task<IActionResult> Delete(int id)
        {
            await _command.DeleteAsync(id);
            return NoContent();
        }
    }
}
