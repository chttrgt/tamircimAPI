namespace TamircimAPI.Models.Interfaces
{
    // Bir tenant'a (teknik servise) ait, izolasyona tabi iş verisi.
    // ApplicationDbContext bu arayüzü gören her entity'ye otomatik tenant filtresi
    // uygular ve insert sırasında TenantId'yi sunucu tarafında (JWT'den) set eder.
    // İstemci TenantId'yi ASLA belirleyemez.
    public interface ITenantOwned
    {
        int TenantId { get; set; }
    }
}
