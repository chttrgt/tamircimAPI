using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using TamircimAPI.Models;
using TamircimAPI.Models.Interfaces;
using TamircimAPI.Services.Tenant;

namespace TamircimAPI.Data
{
    public class ApplicationDbContext : DbContext
    {
        private readonly IHttpContextAccessor? _httpContextAccessor;
        private readonly ITenantContext? _tenantContext;

        // Query filter'larda referans verilen alan — EF, filtre lambda'sını bu instance
        // üzerinden her sorguda yeniden değerlendirir, böylece istek başına tenant uygulanır.
        // Tenant context yoksa (tasarım zamanı/migration) null → filtre satır döndürmez.
        private int CurrentTenantId => _tenantContext?.TenantId ?? 0;

        public static string TurkishLower(string input) =>
            throw new NotSupportedException("Bu metod sadece EF Core LINQ sorgularında kullanılır.");

        // Optimistic concurrency: PostgreSQL'in sistem kolonu xmin'i satır sürümü
        // olarak kullanır. Ekstra kolon/migration gerekmez — xmin her satırda
        // zaten vardır ve her UPDATE'te değişir. Eşzamanlı iki yazım aynı satıra
        // çakışırsa ikincinin UPDATE'i 0 satır etkiler → DbUpdateConcurrencyException.
        private static void UseXminConcurrency(
            Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder entity) =>
            entity.Property<uint>("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsRowVersion();

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            IHttpContextAccessor? httpContextAccessor = null,
            ITenantContext? tenantContext = null)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
            _tenantContext = tenantContext;
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = _httpContextAccessor?.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdClaim, out var userId) ? userId : null;
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            CascadeSoftDelete();
            SetTenantIdFields();
            SetAuditableFields();
            var pendingAuditEntries = CollectAuditEntries();
            var result = base.SaveChanges(acceptAllChangesOnSuccess);
            if (pendingAuditEntries.Count > 0)
            {
                SaveAuditEntries(pendingAuditEntries);
                base.SaveChanges(acceptAllChangesOnSuccess);
            }
            return result;
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            CascadeSoftDelete();
            SetTenantIdFields();
            SetAuditableFields();
            var pendingAuditEntries = CollectAuditEntries();
            var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            if (pendingAuditEntries.Count > 0)
            {
                SaveAuditEntries(pendingAuditEntries);
                await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            }
            return result;
        }

        private static readonly HashSet<string> _auditExcludedTypes = new()
        {
            nameof(AuditLog), nameof(RefreshToken)
        };

        // Tenant izolasyonunun yazma tarafı. İstemci TenantId'yi ASLA belirleyemez:
        // - Kimlikli istekte (context tenant var): yeni satıra context tenant atanır;
        //   kod farklı bir tenant set etmişse → exception (cross-tenant yazma engeli).
        // - Kayıt akışında (context tenant yok): TenantId açıkça set edilmiş olmalıdır
        //   (RegisterAsync yeni tenant'ın id'sini verir); aksi halde → exception.
        // - Mevcut satırın TenantId'sini değiştirmeye çalışmak → exception.
        private void SetTenantIdFields()
        {
            var ctxTenant = _tenantContext?.TenantId;

            foreach (var entry in ChangeTracker.Entries<ITenantOwned>())
            {
                if (entry.State == EntityState.Added)
                {
                    if (ctxTenant.HasValue)
                    {
                        if (entry.Entity.TenantId == 0)
                            entry.Entity.TenantId = ctxTenant.Value;
                        else if (entry.Entity.TenantId != ctxTenant.Value)
                            throw new InvalidOperationException(
                                "Tenant ihlali: kayıt başka bir tenant'a yazılamaz.");
                    }
                    else if (entry.Entity.TenantId == 0)
                    {
                        throw new InvalidOperationException(
                            "Tenant belirlenemedi: TenantId set edilmemiş ve istek tenant bağlamı yok.");
                    }
                }
                else if (entry.State == EntityState.Modified)
                {
                    var tenantProp = entry.Property(nameof(ITenantOwned.TenantId));
                    if (tenantProp.IsModified &&
                        !Equals(tenantProp.OriginalValue, tenantProp.CurrentValue))
                        throw new InvalidOperationException(
                            "Tenant ihlali: var olan kaydın tenant'ı değiştirilemez.");
                }
            }
        }

        private void SetAuditableFields()
        {
            var userId = GetCurrentUserId();

            foreach (var entry in ChangeTracker.Entries<IAuditable>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    if (userId != null && entry.Entity.CreatedByUserId == null)
                        entry.Entity.CreatedByUserId = userId;
                }

                if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    if (userId != null)
                        entry.Entity.UpdatedByUserId = userId;
                }
            }

            // DeletedAt damgası kullanıcıdan BAĞIMSIZ atılır: fotoğraf GC'si DeletedAt'e
            // bakar, dolayısıyla kullanıcısız (sistem) bir silmede bile retention sayacı
            // başlamalı. DeletedByUserId yalnızca kullanıcı biliniyorsa set edilir.
            foreach (var entry in ChangeTracker.Entries<ISoftDeletable>())
            {
                if (entry.State == EntityState.Modified &&
                    entry.Entity.IsDeleted &&
                    entry.Entity.DeletedAt == null)
                {
                    entry.Entity.DeletedAt = DateTime.UtcNow;
                    if (userId != null)
                        entry.Entity.DeletedByUserId = userId;
                }
            }
        }

        // Yalnızca sahiplik ilişkilerini takip et; "kim yaptı" (CreatedBy/UpdatedBy/DeletedBy)
        // bağları SetNull olduğundan otomatik dışarıda kalır.
        private static readonly DeleteBehavior[] _cascadeBehaviors =
            { DeleteBehavior.Cascade, DeleteBehavior.ClientCascade, DeleteBehavior.Restrict };

        private static readonly MethodInfo _setMethod = typeof(DbContext).GetMethods()
            .First(m => m.Name == nameof(DbContext.Set)
                     && m.IsGenericMethodDefinition
                     && m.GetParameters().Length == 0);

        private static readonly MethodInfo _efPropertyMethod =
            typeof(EF).GetMethod(nameof(EF.Property))!;

        // Soft-delete cascade. Bir kayıt IsDeleted false→true geçtiğinde, ona FK ile bağlı
        // (sahiplik: Cascade/Restrict) ve kendisi de ISoftDeletable olan çocukları özyinelemeli
        // soft-delete eder. Müşteri→Cihaz→{ServisKaydı, Fotoğraf} zinciri tek transaction'da akar.
        // Model metadata'sından sürülür → navigation property olmasa bile (örn. DevicePhoto)
        // çocuk bulunur; yeni eklenen sahiplik tabloları otomatik kapsanır. Çocuklar global query
        // filter üzerinden yüklendiğinden tenant-scope ve "zaten silinmiş" hariç tutma bedavadır.
        private void CascadeSoftDelete()
        {
            var roots = ChangeTracker.Entries<ISoftDeletable>()
                .Where(e =>
                {
                    if (e.State != EntityState.Modified) return false;
                    var p = e.Property(nameof(ISoftDeletable.IsDeleted));
                    return p.IsModified && p.CurrentValue is true && p.OriginalValue is false;
                })
                .Select(e => (EntityEntry)e)
                .ToList();

            if (roots.Count == 0) return;

            var queue = new Queue<EntityEntry>(roots);
            var visited = new HashSet<(Type, object)>();

            while (queue.Count > 0)
            {
                var parent = queue.Dequeue();

                var pk = parent.Metadata.FindPrimaryKey();
                if (pk is null || pk.Properties.Count != 1) continue;

                var pkValue = parent.Property(pk.Properties[0].Name).CurrentValue;
                if (pkValue is null) continue;
                if (!visited.Add((parent.Metadata.ClrType, pkValue))) continue;

                foreach (var fk in parent.Metadata.GetReferencingForeignKeys())
                {
                    if (!_cascadeBehaviors.Contains(fk.DeleteBehavior)) continue;

                    var childClrType = fk.DeclaringEntityType.ClrType;
                    if (!typeof(ISoftDeletable).IsAssignableFrom(childClrType)) continue;
                    if (fk.Properties.Count != 1) continue;

                    foreach (var child in LoadLiveChildren(childClrType, fk.Properties[0], pkValue))
                    {
                        var sd = (ISoftDeletable)child;
                        if (sd.IsDeleted) continue;   // güvenlik: query filter zaten eler
                        sd.IsDeleted = true;
                        queue.Enqueue(Entry(child));
                    }
                }
            }
        }

        // Set<TChild>().Where(c => EF.Property<fk>(c, "FkName") == parentKey) sorgusunu
        // çalışma zamanında, tip bilinmeden kurar. Global query filter uygulanır.
        private IEnumerable<object> LoadLiveChildren(Type childClrType, IProperty fkProperty, object parentKeyValue)
        {
            var dbSet = (IQueryable)_setMethod.MakeGenericMethod(childClrType).Invoke(this, null)!;

            var param = Expression.Parameter(childClrType, "c");
            var fkClrType = fkProperty.ClrType;

            Expression fkAccess = Expression.Call(
                _efPropertyMethod.MakeGenericMethod(fkClrType),
                param,
                Expression.Constant(fkProperty.Name));

            Expression key = Expression.Constant(parentKeyValue);
            if (key.Type != fkClrType) key = Expression.Convert(key, fkClrType);

            var predicate = Expression.Lambda(Expression.Equal(fkAccess, key), param);
            var where = Expression.Call(
                typeof(Queryable), nameof(Queryable.Where), new[] { childClrType },
                dbSet.Expression, Expression.Quote(predicate));

            return dbSet.Provider.CreateQuery(where).Cast<object>().ToList();
        }

        private List<(object Entity, string EntityType, string Action, int? UserId, string? ChangedFields)> CollectAuditEntries()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return new();

            var pending = new List<(object Entity, string EntityType, string Action, int? UserId, string? ChangedFields)>();

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State != EntityState.Added &&
                    entry.State != EntityState.Modified &&
                    entry.State != EntityState.Deleted)
                    continue;

                var entityType = entry.Entity.GetType().Name;
                if (_auditExcludedTypes.Contains(entityType)) continue;

                var idProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Id");
                if (idProp == null) continue;

                string action;
                string? changedFields = null;

                if (entry.State == EntityState.Added)
                {
                    action = "Create";
                }
                else if (entry.State == EntityState.Deleted)
                {
                    action = "Delete";
                }
                else
                {
                    var isDeletedProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "IsDeleted");
                    if (isDeletedProp != null && isDeletedProp.IsModified &&
                        isDeletedProp.CurrentValue is true && isDeletedProp.OriginalValue is false)
                    {
                        action = "Delete";
                    }
                    else
                    {
                        action = "Update";
                        var changed = entry.Properties
                            .Where(p => p.IsModified &&
                                        p.Metadata.Name != "UpdatedAt" &&
                                        p.Metadata.Name != "UpdatedByUserId")
                            .Select(p => p.Metadata.Name)
                            .ToList();
                        if (changed.Count > 0)
                            changedFields = string.Join(", ", changed);
                    }
                }

                pending.Add((entry.Entity, entityType, action, userId, changedFields));
            }

            return pending;
        }

        private void SaveAuditEntries(List<(object Entity, string EntityType, string Action, int? UserId, string? ChangedFields)> pendingEntries)
        {
            foreach (var (entity, entityType, action, userId, changedFields) in pendingEntries)
            {
                var idProperty = entity.GetType().GetProperty("Id");
                var entityId = idProperty?.GetValue(entity) is int id ? id : 0;

                AuditLogs.Add(new AuditLog
                {
                    // Audit yalnızca kimlikli isteklerde toplanır (userId != null) →
                    // context tenant her zaman mevcuttur. RLS WITH CHECK için bu satır
                    // doğru tenant'a yazılmalıdır (2. SaveChanges'te SetTenantIdFields
                    // çalışmadığından burada açıkça set ediyoruz).
                    TenantId = _tenantContext?.TenantId ?? 0,
                    EntityType = entityType,
                    EntityId = entityId,
                    Action = action,
                    UserId = userId,
                    Timestamp = DateTime.UtcNow,
                    ChangedFields = changedFields
                });
            }
        }

        #region TENANT
        public DbSet<Tenant> Tenants { get; set; }
        #endregion

        #region KULLANICI
        public DbSet<User> Users { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }
        public DbSet<EmailVerificationToken> EmailVerificationTokens { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
        #endregion

        #region MÜŞTERİ
        public DbSet<Customer> Customers { get; set; }
        #endregion

        #region CİHAZ
        public DbSet<Device> Devices { get; set; }
        #endregion

        #region ARIZA KAYDI
        public DbSet<RepairRecord> RepairRecords { get; set; }
        #endregion

        #region ÖDEME
        public DbSet<Payment> Payments { get; set; }
        #endregion

        #region CİHAZ FOTOĞRAFI
        public DbSet<DevicePhoto> DevicePhotos { get; set; }
        #endregion

        #region DENETİM LOGU
        public DbSet<AuditLog> AuditLogs { get; set; }
        #endregion

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDbFunction(typeof(ApplicationDbContext)
                .GetMethod(nameof(TurkishLower), new[] { typeof(string) })!)
                .HasName("turkish_lower");

            #region Tenant
            modelBuilder.Entity<Tenant>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Branch).HasMaxLength(100);
                entity.Property(e => e.NextDeviceSeq).HasDefaultValue(1L);
                entity.Property(e => e.NextTicketSeq).HasDefaultValue(1L);
                entity.HasIndex(e => e.IsActive);
            });
            #endregion

            #region User
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                // E-posta GLOBAL benzersiz kalır: login e-posta → kullanıcı → tenant
                // ile çalışır, tenant seçicisi gerekmez. Bir e-posta tek tenant'a aittir.
                // Soft-deleted personel hariç → silinen çalışanın e-postası yeniden kullanılabilir.
                entity.HasIndex(e => e.Email).IsUnique().HasFilter("\"IsDeleted\" = false");
                entity.HasIndex(e => e.TenantId);
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
                entity.Property(e => e.Title).HasMaxLength(200);
                entity.Property(e => e.PasswordHash).IsRequired();
                entity.Property(e => e.PasswordSalt).IsRequired();
                entity.Property(e => e.Role).HasConversion<int>();
                entity.Ignore(e => e.FullName);

                entity.HasOne(e => e.Tenant)
                    .WithMany(t => t.Users)
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Tenant izolasyonu (1. katman) + soft-delete elemesi. StaffService vb.
                // otomatik tenant-scope'lu ve silinen personeli görmez; login/refresh
                // global arama için IgnoreQueryFilters() kullanır (orada IsActive ile engellenir).
                entity.HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
            });
            #endregion

            #region EmailVerificationToken
            modelBuilder.Entity<EmailVerificationToken>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Token).IsUnique();
                entity.Property(e => e.Token).IsRequired().HasMaxLength(128);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Ignore(e => e.IsConsumed);
                entity.Ignore(e => e.IsExpired);
                entity.Ignore(e => e.IsValid);
            });
            #endregion

            #region PasswordResetToken
            modelBuilder.Entity<PasswordResetToken>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CodeHash).IsRequired().HasMaxLength(128);
                entity.HasIndex(e => e.UserId);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Ignore(e => e.IsConsumed);
                entity.Ignore(e => e.IsExpired);
                entity.Ignore(e => e.IsValid);
            });
            #endregion

            #region UserPermission
            modelBuilder.Entity<UserPermission>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Permission).IsRequired().HasMaxLength(100);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.Permissions)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Aynı kullanıcıya aynı izin iki kez verilemez.
                entity.HasIndex(e => new { e.UserId, e.Permission }).IsUnique();
            });
            #endregion

            #region RefreshToken
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Token).IsUnique();
                entity.Property(e => e.Token).IsRequired().HasMaxLength(256);
                entity.Property(e => e.CreatedByIp).HasMaxLength(45);
                entity.Property(e => e.RevokedByIp).HasMaxLength(45);
                entity.Property(e => e.ReplacedByToken).HasMaxLength(256);
                entity.Property(e => e.RevokeReason).HasMaxLength(256);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.RefreshTokens)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.ExpiresAt);
                entity.Ignore(e => e.IsExpired);
                entity.Ignore(e => e.IsRevoked);
                entity.Ignore(e => e.IsActive);
            });
            #endregion

            #region Customer
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasKey(e => e.Id);
                UseXminConcurrency(entity); // optimistic concurrency (xmin)
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.NationalId).HasMaxLength(11);
                entity.Property(e => e.Email).HasMaxLength(256);
                entity.Property(e => e.Phone1).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Phone2).HasMaxLength(20);
                entity.Property(e => e.Address).HasColumnType("text");
                entity.Property(e => e.Notes).HasColumnType("text");

                // Benzersizlik tenant-scope'ludur: aynı telefon/kimlik farklı dükkânlarda
                // çakışmaz, ama aynı dükkân içinde tekildir.
                entity.HasIndex(e => new { e.TenantId, e.Phone1 }).IsUnique().HasFilter("\"IsDeleted\" = false");
                entity.HasIndex(e => new { e.TenantId, e.Phone2 }).IsUnique().HasFilter("\"Phone2\" IS NOT NULL AND \"IsDeleted\" = false");
                entity.HasIndex(e => new { e.TenantId, e.NationalId }).IsUnique().HasFilter("\"NationalId\" IS NOT NULL AND \"IsDeleted\" = false");
                entity.HasIndex(e => new { e.TenantId, e.Email }).IsUnique().HasFilter("\"Email\" IS NOT NULL AND \"IsDeleted\" = false");
                entity.Ignore(e => e.FullName);

                entity.HasOne(e => e.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.UpdatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.DeletedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.DeletedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Paging: (TenantId, CreatedAt DESC, Id DESC) composite index.
                entity.HasIndex(e => new { e.TenantId, e.CreatedAt, e.Id });

                entity.HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
            });
            #endregion

            #region Device
            modelBuilder.Entity<Device>(entity =>
            {
                entity.HasKey(e => e.Id);
                UseXminConcurrency(entity); // optimistic concurrency (xmin)
                entity.Property(e => e.DeviceCode).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Brand).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Model).IsRequired().HasMaxLength(200);
                entity.Property(e => e.SerialNumber).HasMaxLength(100);
                entity.Property(e => e.Notes).HasColumnType("text");
                entity.Property(e => e.DeviceType).HasConversion<int>();

                entity.HasIndex(e => new { e.TenantId, e.DeviceCode }).IsUnique();
                entity.HasIndex(e => new { e.TenantId, e.CustomerId });
                entity.HasIndex(e => new { e.TenantId, e.DeviceType });
                entity.HasIndex(e => new { e.TenantId, e.SerialNumber }).HasFilter("\"SerialNumber\" IS NOT NULL");

                entity.HasOne(e => e.Customer)
                    .WithMany(c => c.Devices)
                    .HasForeignKey(e => e.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.UpdatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.DeletedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.DeletedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
            });
            #endregion

            #region RepairRecord
            modelBuilder.Entity<RepairRecord>(entity =>
            {
                entity.HasKey(e => e.Id);
                UseXminConcurrency(entity); // optimistic concurrency (xmin)
                entity.Property(e => e.TicketNo).IsRequired().HasMaxLength(20);
                entity.Property(e => e.FaultDescription).IsRequired().HasColumnType("text");
                entity.Property(e => e.Status).HasConversion<int>();
                entity.Property(e => e.WorkDone).HasColumnType("text");
                entity.Property(e => e.NotRepairedReason).HasColumnType("text");
                entity.Property(e => e.WaitingReason).HasColumnType("text");
                entity.Property(e => e.Notes).HasColumnType("text");
                // Para: numeric(12,2). decimal kullanılır, float/double ASLA.
                entity.Property(e => e.Price).HasColumnType("numeric(12,2)");

                entity.HasIndex(e => new { e.TenantId, e.TicketNo }).IsUnique();
                entity.HasIndex(e => new { e.TenantId, e.DeviceId });
                entity.HasIndex(e => new { e.TenantId, e.Status });
                entity.HasIndex(e => new { e.TenantId, e.ReceivedAt });
                entity.HasIndex(e => new { e.TenantId, e.CreatedAt });

                entity.HasOne(e => e.Device)
                    .WithMany(d => d.RepairRecords)
                    .HasForeignKey(e => e.DeviceId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.UpdatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.DeletedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.DeletedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
            });
            #endregion

            #region Payment
            modelBuilder.Entity<Payment>(entity =>
            {
                entity.HasKey(e => e.Id);
                UseXminConcurrency(entity); // optimistic concurrency (xmin)
                // Para: numeric(12,2). decimal kullanılır, float/double ASLA.
                entity.Property(e => e.Amount).HasColumnType("numeric(12,2)");
                entity.Property(e => e.Method).HasConversion<int>();
                entity.Property(e => e.Note).HasColumnType("text");

                // RepairRecord soft-delete edilince ödemeler de cascade soft-delete olsun
                // (CascadeSoftDelete, Restrict davranışını da kapsar). Hard-delete için
                // Restrict → servis kaydı varken DB seviyesinde ödeme yetim kalmaz.
                entity.HasOne(e => e.RepairRecord)
                    .WithMany(r => r.Payments)
                    .HasForeignKey(e => e.RepairRecordId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.UpdatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.DeletedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.DeletedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Bir servis kaydının ödemelerini hızlı çekmek için.
                entity.HasIndex(e => new { e.TenantId, e.RepairRecordId });

                entity.HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
            });
            #endregion

            #region DevicePhoto
            modelBuilder.Entity<DevicePhoto>(entity =>
            {
                entity.HasKey(e => e.Id);
                UseXminConcurrency(entity); // optimistic concurrency (xmin)
                entity.Property(e => e.FileName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ThumbnailFileName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);

                entity.HasOne(e => e.Device)
                    .WithMany()
                    .HasForeignKey(e => e.DeviceId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.UpdatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.DeletedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.DeletedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => new { e.TenantId, e.DeviceId });
                // GC görevi soft-deleted + retention dolmuş kayıtları DeletedAt ile tarar
                entity.HasIndex(e => new { e.TenantId, e.DeletedAt });
                entity.HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
            });
            #endregion

            #region AuditLog
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Action).IsRequired().HasMaxLength(20);
                entity.Property(e => e.ChangedFields).HasMaxLength(2000);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.DeletedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.DeletedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => new { e.TenantId, e.Timestamp });
                entity.HasIndex(e => new { e.TenantId, e.EntityType, e.EntityId });
                entity.HasIndex(e => new { e.TenantId, e.Action });
                entity.HasIndex(e => e.UserId);

                entity.HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
            });
            #endregion
        }
    }
}
