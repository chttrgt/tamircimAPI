using Microsoft.EntityFrameworkCore;
using TamircimAPI.Data;

namespace TamircimAPI.Services.Common
{
    public class CodeGenerator : ICodeGenerator
    {
        private readonly ApplicationDbContext _db;

        public CodeGenerator(ApplicationDbContext db)
        {
            _db = db;
        }

        // Sabit SQL (SqlQuery + FormattableString, parametre yok) — kullanıcı girdisi yok, EF1002 üretmez.
        public async Task<string> NextDeviceCodeAsync()
        {
            var n = await _db.Database
                .SqlQuery<long>($"SELECT nextval('device_code_seq') AS \"Value\"")
                .FirstAsync();
            return $"CHZ-{n:D6}";
        }

        public async Task<string> NextTicketNoAsync()
        {
            var n = await _db.Database
                .SqlQuery<long>($"SELECT nextval('ticket_no_seq') AS \"Value\"")
                .FirstAsync();
            return $"{DateTime.UtcNow:yy}-{n:D6}";
        }
    }
}
