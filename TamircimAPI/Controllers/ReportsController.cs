using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TamircimAPI.Services.Report;

namespace TamircimAPI.Controllers
{
    // Raporlama yalnızca Owner'a açıktır (sunucu tarafı kapı; istemci gizlemesi yeterli değildir).
    [ApiController]
    [Route("api/reports")]
    [Authorize(Roles = "Owner")]
    public class ReportsController : ControllerBase
    {
        private readonly IReportQueryService _reports;

        public ReportsController(IReportQueryService reports)
        {
            _reports = reports;
        }

        // GET /api/reports/payments?from=2026-06-01&to=2026-06-30  (yerel TR günleri)
        [HttpGet("payments")]
        public async Task<IActionResult> GetPaymentReport([FromQuery] string? from, [FromQuery] string? to)
        {
            if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromDate) ||
                !DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var toDate))
                return BadRequest(new { message = "Geçersiz tarih. Beklenen biçim: yyyy-MM-dd." });

            if (toDate < fromDate)
                return BadRequest(new { message = "Bitiş tarihi başlangıçtan önce olamaz." });
            // Ağır sorgu/kötüye kullanımı sınırla.
            if (toDate.DayNumber - fromDate.DayNumber > 366 * 2)
                return BadRequest(new { message = "Tarih aralığı en fazla 2 yıl olabilir." });

            var result = await _reports.GetPaymentReportAsync(fromDate, toDate);
            return Ok(result);
        }
    }
}
