using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TamircimAPI.Models.DTOs.Customer;
using TamircimAPI.Services.Customer;

namespace TamircimAPI.Controllers
{
    [ApiController]
    [Route("api/customers")]
    [Authorize]
    public class CustomerController : ControllerBase
    {
        private readonly ICustomerQueryService _query;
        private readonly ICustomerCommandService _command;

        public CustomerController(ICustomerQueryService query, ICustomerCommandService command)
        {
            _query = query;
            _command = command;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? search = null)
        {
            var result = await _query.GetAllAsync(search);
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
        public async Task<IActionResult> Create([FromBody] CreateCustomerDTO dto)
        {
            var result = await _command.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateCustomerDTO dto)
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
