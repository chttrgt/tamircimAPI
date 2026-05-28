using Microsoft.EntityFrameworkCore;
using TamircimAPI.Data;
using TamircimAPI.Models.DTOs.Customer;

namespace TamircimAPI.Services.Customer
{
    public class CustomerQueryService : ICustomerQueryService
    {
        private readonly ApplicationDbContext _db;

        public CustomerQueryService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IEnumerable<CustomerListDTO>> GetAllAsync(string? search = null)
        {
            var query = _db.Customers.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(c =>
                    ApplicationDbContext.TurkishLower(c.FirstName + " " + c.LastName).Contains(ApplicationDbContext.TurkishLower(term)) ||
                    c.Phone1.Contains(term) ||
                    (c.NationalId != null && c.NationalId.Contains(term)) ||
                    (c.Email != null && c.Email.Contains(term)));
            }

            return await query
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new CustomerListDTO
                {
                    Id = c.Id,
                    FullName = c.FirstName + " " + c.LastName,
                    Phone1 = c.Phone1,
                    Email = c.Email,
                    NationalId = c.NationalId,
                    CreatedAt = c.CreatedAt,
                    DeviceCount = c.Devices.Count(d => !d.IsDeleted),
                    PrimaryDeviceType = c.Devices
                        .Where(d => !d.IsDeleted)
                        .OrderByDescending(d => d.ReceivedAt)
                        .Select(d => d.DeviceType.ToString())
                        .FirstOrDefault()
                })
                .ToListAsync();
        }

        public async Task<CustomerDTO?> GetByIdAsync(int id)
        {
            var c = await _db.Customers
                .Include(x => x.Devices)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (c == null) return null;

            return new CustomerDTO
            {
                Id = c.Id,
                FirstName = c.FirstName,
                LastName = c.LastName,
                FullName = c.FullName,
                NationalId = c.NationalId,
                Address = c.Address,
                Email = c.Email,
                Phone1 = c.Phone1,
                Phone2 = c.Phone2,
                Notes = c.Notes,
                CreatedAt = c.CreatedAt,
                DeviceCount = c.Devices.Count
            };
        }
    }
}
