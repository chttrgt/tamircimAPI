using Microsoft.EntityFrameworkCore;
using TamircimAPI.Data;
using TamircimAPI.Models.DTOs.Report;

namespace TamircimAPI.Services.Report
{
    // Ödeme raporlama (owner-only). Tüm sorgular EF üzerinden → global query filter
    // (TenantId + !IsDeleted) + RLS otomatik uygulanır; başka tenant'ın verisi görünmez.
    // Toplamlar veritabanında hesaplanır (SUM/COUNT/GROUP BY); satırlar belleğe çekilmez.
    public class ReportQueryService : IReportQueryService
    {
        private readonly ApplicationDbContext _db;

        // Türkiye sabit UTC+3 (DST yok). PaidAt UTC saklandığından yerel güne çevirirken kullanılır.
        private const int TurkeyOffsetHours = 3;

        public ReportQueryService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<PaymentReportDTO> GetPaymentReportAsync(DateOnly from, DateOnly to)
        {
            // Yerel gün sınırlarını UTC pencereye çevir: [from 00:00 TR, (to+1) 00:00 TR).
            var fromUtc = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc).AddHours(-TurkeyOffsetHours);
            var toUtc = DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc).AddHours(-TurkeyOffsetHours);
            var rangeDays = to.DayNumber - from.DayNumber + 1;

            // Aralıktaki tahsilatlar tek sorguda (indeksli WHERE) minimal kolonla çekilir;
            // özet/trend/yöntem bunlardan bellekte türetilir. Yerel-güne göre gruplama SQL'e
            // çevrilmeye çalışılmaz (sağlam ve taşınabilir; tipik aralıkta satır sayısı küçüktür).
            var rangeRows = await _db.Payments
                .Where(p => p.PaidAt >= fromUtc && p.PaidAt < toUtc)
                .Select(p => new { p.PaidAt, p.Amount, p.Method })
                .ToListAsync();

            // ── 1) Özet (+ önceki dönem karşılaştırması) ──
            var total = rangeRows.Sum(x => x.Amount);
            var count = rangeRows.Count;

            var prevFromUtc = fromUtc.AddDays(-rangeDays);
            var prevTotal = await _db.Payments
                .Where(p => p.PaidAt >= prevFromUtc && p.PaidAt < fromUtc)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            double? changePercent;
            if (prevTotal > 0) changePercent = (double)((total - prevTotal) / prevTotal) * 100;
            else changePercent = total > 0 ? null : 0; // baz yok + bu dönem var → "yeni" (null)

            // ── 2) Trend (TR yerel güne göre grupla) ──
            var granularity = rangeDays <= 62 ? "daily" : "monthly";
            List<TrendPointDTO> trend;
            if (granularity == "daily")
            {
                trend = rangeRows
                    .GroupBy(x => DateOnly.FromDateTime(x.PaidAt.AddHours(TurkeyOffsetHours)))
                    .Select(g => new TrendPointDTO
                    {
                        PeriodStart = g.Key.ToDateTime(TimeOnly.MinValue),
                        Label = g.Key.ToString("yyyy-MM-dd"),
                        Total = g.Sum(x => x.Amount),
                        Count = g.Count(),
                    })
                    .OrderBy(x => x.PeriodStart)
                    .ToList();
            }
            else
            {
                trend = rangeRows
                    .GroupBy(x => { var lt = x.PaidAt.AddHours(TurkeyOffsetHours); return new { lt.Year, lt.Month }; })
                    .Select(g => new TrendPointDTO
                    {
                        PeriodStart = new DateTime(g.Key.Year, g.Key.Month, 1),
                        Label = $"{g.Key.Year:D4}-{g.Key.Month:D2}",
                        Total = g.Sum(x => x.Amount),
                        Count = g.Count(),
                    })
                    .OrderBy(x => x.PeriodStart)
                    .ToList();
            }

            // ── 3) Ödeme yöntemi dağılımı ──
            var methods = rangeRows
                .GroupBy(x => x.Method)
                .Select(g => new MethodBreakdownItemDTO { Method = g.Key, Total = g.Sum(x => x.Amount), Count = g.Count() })
                .ToList();

            // ── 4) Alacaklar (anlık; tarih aralığından bağımsız) ──
            // Para sürecin çıpa kaydında toplanır → Price != null olan kayıtlar fiyatlandırılmış işlerdir.
            // Tek sorguda {ücret, tahsilat} çekilir; metrikler bellekte hesaplanır (küçük veri kümesi).
            var pricedRows = await _db.RepairRecords
                .Where(r => r.Price != null)
                .Select(r => new
                {
                    Price = r.Price!.Value,
                    Paid = r.Payments.Sum(p => (decimal?)p.Amount) ?? 0m,
                })
                .ToListAsync();

            var outstanding = new OutstandingSectionDTO
            {
                TotalCharged = pricedRows.Sum(x => x.Price),
                TotalCollected = pricedRows.Sum(x => x.Paid),
                TotalOutstanding = pricedRows.Where(x => x.Paid < x.Price).Sum(x => x.Price - x.Paid),
                UnpaidCount = pricedRows.Count(x => x.Paid <= 0),
                PartialCount = pricedRows.Count(x => x.Paid > 0 && x.Paid < x.Price),
            };

            return new PaymentReportDTO
            {
                FromUtc = fromUtc,
                ToUtc = toUtc,
                Summary = new PaymentSummarySectionDTO
                {
                    TotalPaid = total,
                    PaymentCount = count,
                    AveragePayment = count > 0 ? total / count : 0m,
                    PreviousTotalPaid = prevTotal,
                    ChangePercent = changePercent,
                },
                TrendGranularity = granularity,
                Trend = trend,
                Methods = methods,
                Outstanding = outstanding,
            };
        }
    }
}
