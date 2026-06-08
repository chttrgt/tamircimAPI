namespace TamircimAPI.Authorization
{
    // Fonksiyon-bazlı izin katalogu. Endpoint'ler [HasPermission(Permissions.X)] ile işaretlenir;
    // çalışanlara bu izinler tek tek atanabilir. Görüntüleme (list/get) izin gerektirmez.
    public static class Permissions
    {
        public const string CustomersCreate = "customers.create";
        public const string CustomersEdit   = "customers.edit";
        public const string CustomersDelete = "customers.delete";

        public const string DevicesCreate   = "devices.create";
        public const string DevicesEdit     = "devices.edit";
        public const string DevicesDelete   = "devices.delete";

        public const string RepairsCreate   = "repairs.create";
        public const string RepairsEdit     = "repairs.edit";
        public const string RepairsDelete   = "repairs.delete";

        public const string PhotosCreate     = "photos.create";
        public const string PhotosDelete     = "photos.delete";

        // Çalışana atanabilir tüm izinler. Personel servisi gelen izinleri buna göre doğrular
        // (geçersiz/uydurma izin string'i kaydedilemez).
        public static readonly IReadOnlyList<string> AllAssignable = new[]
        {
            CustomersCreate, CustomersEdit, CustomersDelete,
            DevicesCreate,   DevicesEdit,   DevicesDelete,
            RepairsCreate,   RepairsEdit,   RepairsDelete,
            PhotosCreate,    PhotosDelete,
        };

        public static bool IsValid(string permission) => AllAssignable.Contains(permission);
    }
}
