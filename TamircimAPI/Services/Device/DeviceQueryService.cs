using Microsoft.EntityFrameworkCore;
using TamircimAPI.Data;
using TamircimAPI.Models.DTOs.Device;
using TamircimAPI.Models.DTOs.Repair;
using TamircimAPI.Models.Enums;

namespace TamircimAPI.Services.Device
{
    public class DeviceQueryService : IDeviceQueryService
    {
        private readonly ApplicationDbContext _db;

        public DeviceQueryService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IEnumerable<DeviceListDTO>> GetAllAsync(int? customerId = null, string? search = null, string? filter = null)
        {
            var query = _db.Devices.Include(d => d.Customer).Include(d => d.RepairRecords).AsQueryable();

            if (customerId.HasValue)
                query = query.Where(d => d.CustomerId == customerId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(d =>
                    d.DeviceCode.Contains(term) ||
                    d.Brand.Contains(term) ||
                    d.Model.Contains(term) ||
                    (d.SerialNumber != null && d.SerialNumber.Contains(term)));
            }

            var devices = await query
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            // Açık iş filtreleri — Dashboard ile aynı "güncel durum = en son kayıt" mantığı.
            if (filter == "active")
            {
                // Aktif onarım = açık (en son kayıt teslim edilmemiş) + güncel durumu Beklemede.
                devices = devices
                    .Select(d => (Device: d, Visit: OpenVisit(d)))
                    .Where(x => x.Visit is { } v && v.status == RepairStatus.Waiting)
                    .OrderByDescending(x => x.Visit!.Value.openSince)
                    .Select(x => x.Device)
                    .ToList();
            }
            else if (filter == "overdue")
            {
                // Gecikmiş = açık (durum fark etmez) + bu gelişin başlangıcı 7 günden eski.
                var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
                devices = devices
                    .Select(d => (Device: d, Visit: OpenVisit(d)))
                    .Where(x => x.Visit is { } v && v.openSince < sevenDaysAgo)
                    .OrderBy(x => x.Visit!.Value.openSince)
                    .Select(x => x.Device)
                    .ToList();
            }

            return devices.Select(MapToListDTO);
        }

        public async Task<DevicePagedDTO> GetPagedAsync(int? customerId, string? search, string? filter, int page, int pageSize)
        {
            var query = _db.Devices.Include(d => d.Customer).Include(d => d.RepairRecords).AsQueryable();

            if (customerId.HasValue)
                query = query.Where(d => d.CustomerId == customerId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(d =>
                    d.DeviceCode.Contains(term) ||
                    d.Brand.Contains(term) ||
                    d.Model.Contains(term) ||
                    (d.SerialNumber != null && d.SerialNumber.Contains(term)));
            }

            var devices = await query.OrderByDescending(d => d.CreatedAt).ToListAsync();

            if (filter == "active")
            {
                devices = devices
                    .Select(d => (Device: d, Visit: OpenVisit(d)))
                    .Where(x => x.Visit is { } v && v.status == RepairStatus.Waiting)
                    .OrderByDescending(x => x.Visit!.Value.openSince)
                    .Select(x => x.Device).ToList();
            }
            else if (filter == "overdue")
            {
                var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
                devices = devices
                    .Select(d => (Device: d, Visit: OpenVisit(d)))
                    .Where(x => x.Visit is { } v && v.openSince < sevenDaysAgo)
                    .OrderBy(x => x.Visit!.Value.openSince)
                    .Select(x => x.Device).ToList();
            }

            var total = devices.Count;
            var items = devices.Skip((page - 1) * pageSize).Take(pageSize).Select(MapToListDTO).ToList();

            return new DevicePagedDTO { Items = items, HasMore = page * pageSize < total };
        }

        // Cihazın AÇIK ziyaret bilgisi: en son kayıt teslim edilmemişse o ziyaretin başlangıç
        // tarihi (son teslimden sonraki ilk kayıt) ve güncel durum. Kapalıysa null.
        private static (DateTime openSince, RepairStatus status)? OpenVisit(Models.Device d)
        {
            var ordered = d.RepairRecords.OrderBy(r => r.ReceivedAt).ToList();
            if (ordered.Count == 0) return null;
            var latest = ordered[^1];
            if (latest.IsDelivered) return null;
            var lastDeliveredIdx = ordered.FindLastIndex(r => r.IsDelivered);
            return (ordered[lastDeliveredIdx + 1].ReceivedAt, latest.Status);
        }

        public async Task<DeviceDTO?> GetByIdAsync(int id)
        {
            var d = await _db.Devices
                .Include(x => x.Customer)
                .Include(x => x.RepairRecords)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (d == null) return null;

            var dto = MapToDetailDTO(d);
            dto.PhotoCount = await _db.DevicePhotos.CountAsync(p => p.DeviceId == id);
            return dto;
        }

        public async Task<DeviceDTO?> GetByCodeAsync(string code)
        {
            var term = code.Trim();
            if (string.IsNullOrEmpty(term)) return null;

            var d = await _db.Devices
                .Include(x => x.Customer)
                .Include(x => x.RepairRecords)
                .FirstOrDefaultAsync(x => x.DeviceCode.ToLower() == term.ToLower());

            if (d == null) return null;

            var dto = MapToDetailDTO(d);
            dto.PhotoCount = await _db.DevicePhotos.CountAsync(p => p.DeviceId == d.Id);
            return dto;
        }

        private static DeviceDTO MapToDetailDTO(Models.Device d)
        {
            var latest = d.RepairRecords.OrderByDescending(r => r.ReceivedAt).FirstOrDefault();

            return new DeviceDTO
            {
                Id = d.Id,
                CustomerId = d.CustomerId,
                CustomerFullName = d.Customer.FullName,
                DeviceCode = d.DeviceCode,
                DeviceType = d.DeviceType,
                DeviceTypeLabel = GetDeviceTypeLabel(d.DeviceType),
                DeviceName = d.DeviceName,
                Brand = d.Brand,
                Model = d.Model,
                SerialNumber = d.SerialNumber,
                ExtraFields = d.ExtraFields,
                Notes = d.Notes,
                CreatedAt = d.CreatedAt,
                RepairCount = d.RepairRecords.Count,
                LastReceivedAt = latest?.ReceivedAt,
                CurrentStatus = latest != null ? GetStatusLabel(latest.Status) : null,
                HasOpenTicket = d.RepairRecords.Any(r => !r.IsDelivered)
            };
        }

        public async Task<IEnumerable<CustomerVisitDTO>> GetCustomerHistoryAsync(int customerId)
        {
            var devices = await _db.Devices
                .Include(d => d.RepairRecords)
                .Where(d => d.CustomerId == customerId)
                .ToListAsync();

            var visits = new List<CustomerVisitDTO>();

            // Geçmiş = ZİYARET başına bir satır. Bir ziyaret, cihazın o gelişinde açılan
            // tüm kayıtları (intake + durum güncellemeleri, Waiting dahil) kapsar ve kendi
            // son durumuyla gösterilir. Yeni bir ziyaret (n. geliş) yalnızca önceki ziyaret
            // TESLİM edildikten sonra açılan ilk kayıtla başlar — Waiting durumu yeni ziyaret
            // başlatmaz. Tüm işlem süreci cihaz detayındaki Servis Kayıtları'nda durur.
            foreach (var d in devices)
            {
                var ordered = d.RepairRecords.OrderBy(r => r.ReceivedAt).ToList();
                if (ordered.Count == 0) continue;

                // Ziyaretlere grupla — sınır yalnızca teslim (IsDelivered)
                var groups = new List<List<Models.RepairRecord>>();
                foreach (var r in ordered)
                {
                    if (groups.Count == 0 || groups[^1][^1].IsDelivered)
                        groups.Add(new List<Models.RepairRecord> { r });
                    else
                        groups[^1].Add(r);
                }

                var totalVisits = groups.Count;
                for (var gi = 0; gi < groups.Count; gi++)
                {
                    var first = groups[gi][0];      // geliş (intake)
                    var last = groups[gi][^1];      // son işlem — durum buradan

                    visits.Add(new CustomerVisitDTO
                    {
                        RepairRecordId = last.Id,
                        DeviceId = d.Id,
                        TicketNo = last.TicketNo,
                        DeviceCode = d.DeviceCode,
                        DeviceName = d.DeviceName,
                        Brand = d.Brand,
                        Model = d.Model,
                        SerialNumber = d.SerialNumber,
                        DeviceType = d.DeviceType,
                        DeviceTypeLabel = GetDeviceTypeLabel(d.DeviceType),
                        FaultDescription = last.FaultDescription,
                        ReceivedAt = first.ReceivedAt,     // geliş tarihi
                        LastActionAt = last.ReceivedAt,    // son işlem tarihi
                        IsDelivered = last.IsDelivered,
                        DeliveredAt = last.DeliveredAt,
                        Status = last.Status,
                        StatusLabel = GetStatusLabel(last.Status),
                        StatusDetail = last.Status switch
                        {
                            RepairStatus.Repaired => last.WorkDone,
                            RepairStatus.NotRepaired => last.NotRepairedReason,
                            _ => last.WaitingReason
                        },
                        Notes = last.Notes,
                        VisitNo = gi + 1,
                        TotalVisits = totalVisits
                    });
                }
            }

            // En son işlem yapılan ziyaret en üstte
            return visits.OrderByDescending(v => v.LastActionAt).ToList();
        }

        public async Task<SerialCheckResultDTO> CheckSerialAsync(string serialNumber, int? excludeDeviceId = null)
        {
            var term = serialNumber.Trim();
            if (string.IsNullOrEmpty(term))
                return new SerialCheckResultDTO { Exists = false };

            var match = await _db.Devices
                .Include(d => d.Customer)
                .Where(d => d.SerialNumber != null && d.SerialNumber.ToLower() == term.ToLower())
                .Where(d => excludeDeviceId == null || d.Id != excludeDeviceId.Value)
                .OrderByDescending(d => d.CreatedAt)
                .FirstOrDefaultAsync();

            if (match == null)
                return new SerialCheckResultDTO { Exists = false };

            return new SerialCheckResultDTO
            {
                Exists = true,
                DeviceId = match.Id,
                DeviceCode = match.DeviceCode,
                CustomerId = match.CustomerId,
                CustomerFullName = match.Customer.FullName
            };
        }

        private static DeviceListDTO MapToListDTO(Models.Device d)
        {
            var latest = d.RepairRecords.OrderByDescending(r => r.ReceivedAt).FirstOrDefault();
            return new DeviceListDTO
            {
                Id = d.Id,
                CustomerId = d.CustomerId,
                CustomerFullName = d.Customer.FullName,
                DeviceCode = d.DeviceCode,
                DeviceName = d.DeviceName,
                Brand = d.Brand,
                Model = d.Model,
                SerialNumber = d.SerialNumber,
                DeviceType = d.DeviceType,
                DeviceTypeLabel = GetDeviceTypeLabel(d.DeviceType),
                RepairCount = d.RepairRecords.Count,
                LastReceivedAt = latest?.ReceivedAt,
                CurrentStatus = latest != null ? GetStatusLabel(latest.Status) : null,
                HasOpenTicket = d.RepairRecords.Any(r => !r.IsDelivered)
            };
        }

        private static string GetDeviceTypeLabel(DeviceType type) => type switch
        {
            DeviceType.WhiteGoods => "Beyaz Eşya",
            DeviceType.Phone => "Telefon",
            DeviceType.Electronics => "Elektronik",
            DeviceType.Other => "Diğer",
            _ => "Bilinmiyor"
        };

        private static string GetStatusLabel(RepairStatus status) => status switch
        {
            RepairStatus.Waiting => "Beklemede",
            RepairStatus.Repaired => "Onarıldı",
            RepairStatus.NotRepaired => "Onarılmadı",
            _ => "Bilinmiyor"
        };
    }
}
