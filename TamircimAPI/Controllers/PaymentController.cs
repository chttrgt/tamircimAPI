using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TamircimAPI.Authorization;
using TamircimAPI.Models.DTOs.Payment;
using TamircimAPI.Services.Payment;

namespace TamircimAPI.Controllers
{
    // Ödemeler bir servis kaydına (RepairRecord) bağlıdır → liste/ekleme route'u
    // repair altında iç içedir. Silme tek bir ödeme id'siyle yapılır.
    [ApiController]
    [Route("api/repairs/{repairId:int}/payments")]
    [Authorize]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentQueryService _query;
        private readonly IPaymentCommandService _command;

        public PaymentController(IPaymentQueryService query, IPaymentCommandService command)
        {
            _query = query;
            _command = command;
        }

        [HttpGet]
        public async Task<IActionResult> GetByRepair(int repairId)
        {
            var result = await _query.GetByRepairAsync(repairId);
            return Ok(result);
        }

        [HttpPost]
        [HasPermission(Permissions.PaymentsCreate)]
        public async Task<IActionResult> Add(int repairId, [FromBody] CreatePaymentDTO dto)
        {
            var result = await _command.AddAsync(repairId, dto);
            return Ok(result);
        }

        // DELETE /api/payments/{paymentId} — controller route prefix'ini ~/ ile geçersiz kılar.
        [HttpDelete("~/api/payments/{paymentId:int}")]
        [HasPermission(Permissions.PaymentsDelete)]
        public async Task<IActionResult> Delete(int paymentId)
        {
            await _command.DeleteAsync(paymentId);
            return NoContent();
        }
    }
}
