using TamircimAPI.Models.DTOs.Payment;
using TamircimAPI.Models.Enums;

namespace TamircimAPI.Models.DTOs.Repair
{
    public class RepairRecordDTO
    {
        public int Id { get; set; }
        public int DeviceId { get; set; }
        public string TicketNo { get; set; } = string.Empty;
        public string DeviceCode { get; set; } = string.Empty;
        public string DeviceBrand { get; set; } = string.Empty;
        public string DeviceModel { get; set; } = string.Empty;
        public string CustomerFullName { get; set; } = string.Empty;
        public string FaultDescription { get; set; } = string.Empty;
        public DateTime ReceivedAt { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public bool IsDelivered { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public RepairStatus Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public string? WorkDone { get; set; }
        public string? NotRepairedReason { get; set; }
        public string? WaitingReason { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? Notes { get; set; }
        // Ücret/ödeme. Remaining ve PaymentStatus, Price ile TotalPaid'den hesaplanır.
        public decimal? Price { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal? Remaining { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public List<PaymentDTO> Payments { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // Var olan bir cihaza yeni servis kaydı (geliş) açar.
    public class CreateRepairRecordDTO
    {
        public int DeviceId { get; set; }
        public string FaultDescription { get; set; } = string.Empty;
        public DateTime? ReceivedAt { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public RepairStatus Status { get; set; } = RepairStatus.Waiting;
        public string? WorkDone { get; set; }
        public string? NotRepairedReason { get; set; }
        public string? WaitingReason { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? Notes { get; set; }
        // Not: Ücret burada YOK. Para (ücret + tahsilat) tek kayda değil onarım sürecine
        // aittir → yalnızca PATCH /repairs/{id}/price (SetPriceDTO) ile, sürecin çıpa
        // kaydı üzerinden yönetilir. Böylece durum güncellemesi ücreti yanlışlıkla silmez.
    }

    public class MarkDeliveredDTO
    {
        public DateTime? DeliveredAt { get; set; }
    }

    // Yalnızca anlaşılan ücreti günceller (tahsilat ekranından hızlı düzenleme).
    // null → fiyat temizlenir/belirlenmemiş sayılır. Diğer alanlara dokunulmaz.
    public class SetPriceDTO
    {
        public decimal? Price { get; set; }
    }

    public class UpdateRepairRecordDTO
    {
        public string FaultDescription { get; set; } = string.Empty;
        public DateTime? ReceivedAt { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public RepairStatus Status { get; set; }
        public string? WorkDone { get; set; }
        public string? NotRepairedReason { get; set; }
        public string? WaitingReason { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? Notes { get; set; }
        // Ücret bu DTO'da YOK; bkz CreateRepairRecordDTO notu — yalnızca SetPrice ile yönetilir.
    }

    public class RepairRecordListDTO
    {
        public int Id { get; set; }
        public int DeviceId { get; set; }
        public string TicketNo { get; set; } = string.Empty;
        public string DeviceCode { get; set; } = string.Empty;
        public string DeviceBrand { get; set; } = string.Empty;
        public string DeviceModel { get; set; } = string.Empty;
        public string CustomerFullName { get; set; } = string.Empty;
        public string FaultDescription { get; set; } = string.Empty;
        public DateTime ReceivedAt { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public bool IsDelivered { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public RepairStatus Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public string? WorkDone { get; set; }
        public string? NotRepairedReason { get; set; }
        public string? WaitingReason { get; set; }
        public string? Notes { get; set; }
        // Ücret/ödeme özeti (liste için; ödeme satırları dahil edilmez)
        public decimal? Price { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal? Remaining { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    // Müşteri geçmişi için: bir gelişin cihaz bilgisiyle birlikte özeti.
    public class CustomerVisitDTO
    {
        public int RepairRecordId { get; set; }
        public int DeviceId { get; set; }
        public string TicketNo { get; set; } = string.Empty;
        public string DeviceCode { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string? SerialNumber { get; set; }
        public DeviceType DeviceType { get; set; }
        public string DeviceTypeLabel { get; set; } = string.Empty;
        public string FaultDescription { get; set; } = string.Empty;
        // Ziyaretin ilk gelişi (intake) ve son işlem tarihi
        public DateTime ReceivedAt { get; set; }
        public DateTime LastActionAt { get; set; }
        public bool IsDelivered { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public RepairStatus Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        // Duruma göre neden/açıklama (yapılan iş / onarılamama / bekleme nedeni)
        public string? StatusDetail { get; set; }
        // Duruma ilişkin not
        public string? Notes { get; set; }
        // Ödeme özeti — ücret ziyaretin çıpa (geliş) kaydında tutulur, tahsilat ziyaret
        // boyunca toplanır. Kalan/durum Price + TotalPaid'den türetilir (bkz PaymentCalculator).
        public decimal? Price { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal? Remaining { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        // Bu cihazın kaçıncı ziyareti ve toplam ziyaret sayısı
        public int VisitNo { get; set; }
        public int TotalVisits { get; set; }
    }
}
