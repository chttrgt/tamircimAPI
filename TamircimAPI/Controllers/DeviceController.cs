using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        public async Task<IActionResult> GetAll([FromQuery] int? customerId = null, [FromQuery] string? search = null)
        {
            var result = await _query.GetAllAsync(customerId, search);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _query.GetByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateDeviceDTO dto)
        {
            var result = await _command.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateDeviceDTO dto)
        {
            var result = await _command.UpdateAsync(id, dto);
            return Ok(result);
        }

        [HttpPatch("{id:int}/deliver")]
        public async Task<IActionResult> MarkDelivered(int id, [FromBody] MarkDeliveredDTO? dto = null)
        {
            var result = await _command.MarkDeliveredAsync(id, dto?.DeliveredAt);
            return Ok(result);
        }

        [HttpPatch("{id:int}/undeliver")]
        public async Task<IActionResult> UndoDelivery(int id)
        {
            var result = await _command.UndoDeliveryAsync(id);
            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _command.DeleteAsync(id);
            return NoContent();
        }
    }
}
