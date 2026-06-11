using TamircimAPI.Models.Enums;

namespace TamircimAPI.Models.DTOs.Report
{
    // Ödeme raporu — owner-only. Tüm tutarlar tek kiracıya aittir (global query filter + RLS).
    // Para verisi sürecin çıpa kaydında toplanır (Price != null = fiyatlandırılmış iş).
    public class PaymentReportDTO
    {
        // Sorgulanan UTC penceresi (yerel gün sınırlarından türetilir).
        public DateTime FromUtc { get; set; }
        public DateTime ToUtc { get; set; }

        public PaymentSummarySectionDTO Summary { get; set; } = new();
        // "daily" (≤62 gün) veya "monthly" — istemci eksen etiketini buna göre biçimler.
        public string TrendGranularity { get; set; } = "daily";
        public List<TrendPointDTO> Trend { get; set; } = new();
        public List<MethodBreakdownItemDTO> Methods { get; set; } = new();
        // Alacaklar anlık durumdur — tarih aralığından BAĞIMSIZ.
        public OutstandingSectionDTO Outstanding { get; set; } = new();
    }

    public class PaymentSummarySectionDTO
    {
        public decimal TotalPaid { get; set; }
        public int PaymentCount { get; set; }
        public decimal AveragePayment { get; set; }
        public decimal PreviousTotalPaid { get; set; }
        // Önceki döneme göre % değişim. Önceki dönem 0 ve bu dönem >0 ise null (baz yok → "yeni").
        public double? ChangePercent { get; set; }
    }

    public class TrendPointDTO
    {
        public DateTime PeriodStart { get; set; }
        public string Label { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public int Count { get; set; }
    }

    public class MethodBreakdownItemDTO
    {
        public PaymentMethod Method { get; set; }
        public decimal Total { get; set; }
        public int Count { get; set; }
    }

    public class OutstandingSectionDTO
    {
        public decimal TotalCharged { get; set; }    // toplam ücretlendirilen (Price)
        public decimal TotalCollected { get; set; }  // fiyatlandırılmış işlerde toplam tahsilat
        public decimal TotalOutstanding { get; set; }// kalan (Price - tahsilat), yalnızca pozitifler
        public int UnpaidCount { get; set; }         // fiyatlandırılmış ama hiç ödenmemiş iş sayısı
        public int PartialCount { get; set; }        // kısmi ödenmiş iş sayısı
    }
}
