using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TamircimAPI.Data
{
    // Yalnızca tasarım-zamanı (`dotnet ef migrations add` / `database update`) için.
    // Program.cs'i (JWT/CORS/bağlantı env değişkenleri) çalıştırmadan DbContext kurar;
    // böylece migration üretimi ortamdan bağımsız ve tekrarlanabilir olur.
    // Gerçek bağlantı AÇILMAZ — model kodtan üretilir; yer-tutucu dize yalnızca
    // Npgsql sağlayıcısını seçmeye yarar.
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            // Runtime ile aynı timestamp davranışı → üretilen DDL "timestamp" (timestamptz
            // değil) kullanır. Program.cs'teki ayarla birebir aynı olmalı.
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql("Host=localhost;Port=5432;Database=design_time;Username=postgres;Password=postgres")
                .Options;

            return new ApplicationDbContext(options);
        }
    }
}
