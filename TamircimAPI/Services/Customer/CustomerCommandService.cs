using Microsoft.EntityFrameworkCore;
using TamircimAPI.Data;
using TamircimAPI.Models.DTOs.Customer;

namespace TamircimAPI.Services.Customer
{
    public class CustomerCommandService : ICustomerCommandService
    {
        private readonly ApplicationDbContext _db;
        private readonly ICustomerQueryService _query;

        public CustomerCommandService(ApplicationDbContext db, ICustomerQueryService query)
        {
            _db = db;
            _query = query;
        }

        public async Task<CustomerDTO> CreateAsync(CreateCustomerDTO dto)
        {
            var customer = new Models.Customer
            {
                FirstName = dto.FirstName.Trim(),
                LastName = dto.LastName.Trim(),
                NationalId = dto.NationalId?.Trim(),
                Address = dto.Address?.Trim(),
                Email = dto.Email?.Trim(),
                Phone1 = dto.Phone1.Trim(),
                Phone2 = dto.Phone2?.Trim(),
                Notes = dto.Notes?.Trim()
            };

            _db.Customers.Add(customer);
            await _db.SaveChangesAsync();

            return (await _query.GetByIdAsync(customer.Id))!;
        }

        public async Task<CustomerDTO> UpdateAsync(int id, UpdateCustomerDTO dto)
        {
            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id)
                ?? throw new KeyNotFoundException($"Müşteri bulunamadı: {id}");

            customer.FirstName = dto.FirstName.Trim();
            customer.LastName = dto.LastName.Trim();
            customer.NationalId = dto.NationalId?.Trim();
            customer.Address = dto.Address?.Trim();
            customer.Email = dto.Email?.Trim();
            customer.Phone1 = dto.Phone1.Trim();
            customer.Phone2 = dto.Phone2?.Trim();
            customer.Notes = dto.Notes?.Trim();

            await _db.SaveChangesAsync();

            return (await _query.GetByIdAsync(id))!;
        }

        public async Task DeleteAsync(int id)
        {
            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id)
                ?? throw new KeyNotFoundException($"Müşteri bulunamadı: {id}");

            customer.IsDeleted = true;
            await _db.SaveChangesAsync();
        }
    }
}
