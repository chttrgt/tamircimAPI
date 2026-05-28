using Microsoft.EntityFrameworkCore;
using TamircimAPI.Data;
using TamircimAPI.Exceptions;
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

        private async Task EnsureUniqueFieldsAsync(string phone1, string? phone2, string? nationalId, string? email, int? excludeId = null)
        {
            if (!string.IsNullOrEmpty(phone2) && phone1 == phone2)
                throw new BusinessRuleException(
                    "Telefon 1 ve Telefon 2 aynı olamaz.",
                    "PHONES_ARE_EQUAL");

            var phoneExists = await _db.Customers.AnyAsync(c =>
                c.Phone1 == phone1 && (excludeId == null || c.Id != excludeId));
            if (phoneExists)
                throw new BusinessRuleException(
                    "Bu telefon numarası başka bir müşteriye kayıtlıdır.",
                    "PHONE_ALREADY_EXISTS");

            if (!string.IsNullOrEmpty(phone2))
            {
                var phone2Exists = await _db.Customers.AnyAsync(c =>
                    c.Phone2 == phone2 && (excludeId == null || c.Id != excludeId));
                if (phone2Exists)
                    throw new BusinessRuleException(
                        "Bu telefon numarası başka bir müşteriye kayıtlıdır.",
                        "PHONE2_ALREADY_EXISTS");
            }

            if (!string.IsNullOrEmpty(nationalId))
            {
                var tcExists = await _db.Customers.AnyAsync(c =>
                    c.NationalId == nationalId && (excludeId == null || c.Id != excludeId));
                if (tcExists)
                    throw new BusinessRuleException(
                        "Bu TC Kimlik No başka bir müşteriye kayıtlıdır.",
                        "NATIONAL_ID_ALREADY_EXISTS");
            }

            if (!string.IsNullOrEmpty(email))
            {
                var emailExists = await _db.Customers.AnyAsync(c =>
                    c.Email == email && (excludeId == null || c.Id != excludeId));
                if (emailExists)
                    throw new BusinessRuleException(
                        "Bu e-posta adresi başka bir müşteriye kayıtlıdır.",
                        "EMAIL_ALREADY_EXISTS");
            }
        }

        public async Task<CustomerDTO> CreateAsync(CreateCustomerDTO dto)
        {
            await EnsureUniqueFieldsAsync(dto.Phone1.Trim(), dto.Phone2?.Trim(), dto.NationalId?.Trim(), dto.Email?.Trim());

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

            await EnsureUniqueFieldsAsync(dto.Phone1.Trim(), dto.Phone2?.Trim(), dto.NationalId?.Trim(), dto.Email?.Trim(), excludeId: id);

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
