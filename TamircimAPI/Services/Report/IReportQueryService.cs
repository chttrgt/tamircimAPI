using TamircimAPI.Models.DTOs.Report;

namespace TamircimAPI.Services.Report
{
    public interface IReportQueryService
    {
        // Yerel (TR) gün sınırlarıyla verilen aralık için ödeme raporu üretir.
        Task<PaymentReportDTO> GetPaymentReportAsync(DateOnly from, DateOnly to);
    }
}
