namespace TamircimAPI.Models
{
    // Bir teknik servis (dükkân) = bir tenant. Tüm iş verisi (müşteri, cihaz, kayıt)
    // bir tenant'a aittir ve tenant'lar arası kesinlikle izoledir.
    // İlk kaydolan kullanıcı tenant'ın sahibidir (Owner); personeller (Employee)
    // aynı tenant altında yer alır.
    public class Tenant
    {
        public int Id { get; set; }

        // Dükkân/servis adı.
        public string Name { get; set; } = string.Empty;

        // Cihaz tipi varsayılanı (önceden User.Branch'teydi). Yeni cihaz oluşturulurken
        // tip bundan türetilir → dükkân genelinde tutarlı.
        public string Branch { get; set; } = string.Empty;

        // E-posta doğrulanana kadar false → bu tenant'ın kullanıcıları giriş yapamaz.
        public bool IsActive { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Per-tenant numaralandırma sayaçları. Atomik UPDATE ... RETURNING ile arttırılır
        // (CodeGenerator). Her tenant kendi serisini 1'den görür; tenant'lar çakışmaz.
        public long NextDeviceSeq { get; set; } = 1;
        public long NextTicketSeq { get; set; } = 1;

        public ICollection<User> Users { get; set; } = new List<User>();
    }
}
