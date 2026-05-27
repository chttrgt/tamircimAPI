using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TamircimAPI.Models.DTOs.Repair;
using TamircimAPI.Models.Enums;
using TamircimAPI.Services.Repair;

namespace TamircimAPI.Controllers
{
    [ApiController]
    [Route("api/repairs")]
    [Authorize]
    public class RepairController : ControllerBase
    {
        private readonly IRepairQueryService _query;
        private readonly IRepairCommandService _command;

        public RepairController(IRepairQueryService query, IRepairCommandService command)
        {
            _query = query;
            _command = command;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int? deviceId = null, [FromQuery] RepairStatus? status = null)
        {
            var result = await _query.GetAllAsync(deviceId, status);
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
        public async Task<IActionResult> Create([FromBody] CreateRepairRecordDTO dto)
        {
            var result = await _command.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateRepairRecordDTO dto)
        {
            var result = await _command.UpdateAsync(id, dto);
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
