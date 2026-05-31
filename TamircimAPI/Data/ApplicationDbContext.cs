using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TamircimAPI.Models;
using TamircimAPI.Models.Interfaces;

namespace TamircimAPI.Data
{
    public class ApplicationDbContext : DbContext
    {
        private readonly IHttpContextAccessor? _httpContextAccessor;

        public static string TurkishLower(string input) =>
            throw new NotSupportedException("Bu metod sadece EF Core LINQ sorgularında kullanılır.");

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor? httpContextAccessor = null)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = _httpContextAccessor?.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdClaim, out var userId) ? userId : null;
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
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

            if (userId == null) return;

            foreach (var entry in ChangeTracker.Entries<ISoftDeletable>())
            {
                if (entry.State == EntityState.Modified &&
                    entry.Entity.IsDeleted &&
                    entry.Entity.DeletedByUserId == null)
                {
                    entry.Entity.DeletedAt = DateTime.UtcNow;
                    entry.Entity.DeletedByUserId = userId;
                }
            }
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
                    EntityType = entityType,
                    EntityId = entityId,
                    Action = action,
                    UserId = userId,
                    Timestamp = DateTime.UtcNow,
                    ChangedFields = changedFields
                });
            }
        }

        #region KULLANICI
        public DbSet<User> Users { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
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

        #region DENETİM LOGU
        public DbSet<AuditLog> AuditLogs { get; set; }
        #endregion

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDbFunction(typeof(ApplicationDbContext)
                .GetMethod(nameof(TurkishLower), new[] { typeof(string) })!)
                .HasName("turkish_lower");

            #region User
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
                entity.Property(e => e.Title).HasMaxLength(200);
                entity.Property(e => e.PasswordHash).IsRequired();
                entity.Property(e => e.PasswordSalt).IsRequired();
                entity.Ignore(e => e.FullName);
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
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.NationalId).HasMaxLength(11);
                entity.Property(e => e.Email).HasMaxLength(256);
                entity.Property(e => e.Phone1).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Phone2).HasMaxLength(20);
                entity.Property(e => e.Address).HasColumnType("text");
                entity.Property(e => e.Notes).HasColumnType("text");

                entity.HasIndex(e => e.Phone1).IsUnique().HasFilter("\"IsDeleted\" = false");
                entity.HasIndex(e => e.Phone2).IsUnique().HasFilter("\"Phone2\" IS NOT NULL AND \"IsDeleted\" = false");
                entity.HasIndex(e => e.NationalId).IsUnique().HasFilter("\"NationalId\" IS NOT NULL AND \"IsDeleted\" = false");
                entity.HasIndex(e => e.Email).IsUnique().HasFilter("\"Email\" IS NOT NULL AND \"IsDeleted\" = false");
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

                entity.HasQueryFilter(e => !e.IsDeleted);
                entity.HasIndex(e => e.IsDeleted).HasFilter("\"IsDeleted\" = false");
            });
            #endregion

            #region Device
            modelBuilder.Entity<Device>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.DeviceCode).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Brand).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Model).IsRequired().HasMaxLength(200);
                entity.Property(e => e.SerialNumber).HasMaxLength(100);
                entity.Property(e => e.Notes).HasColumnType("text");
                entity.Property(e => e.DeviceType).HasConversion<int>();

                entity.HasIndex(e => e.DeviceCode).IsUnique();
                entity.HasIndex(e => e.CustomerId);
                entity.HasIndex(e => e.DeviceType);
                entity.HasIndex(e => e.SerialNumber).HasFilter("\"SerialNumber\" IS NOT NULL");

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

                entity.HasQueryFilter(e => !e.IsDeleted);
                entity.HasIndex(e => e.IsDeleted).HasFilter("\"IsDeleted\" = false");
            });
            #endregion

            #region RepairRecord
            modelBuilder.Entity<RepairRecord>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TicketNo).IsRequired().HasMaxLength(20);
                entity.Property(e => e.FaultDescription).IsRequired().HasColumnType("text");
                entity.Property(e => e.Status).HasConversion<int>();
                entity.Property(e => e.WorkDone).HasColumnType("text");
                entity.Property(e => e.NotRepairedReason).HasColumnType("text");
                entity.Property(e => e.WaitingReason).HasColumnType("text");
                entity.Property(e => e.Notes).HasColumnType("text");

                entity.HasIndex(e => e.TicketNo).IsUnique();
                entity.HasIndex(e => e.DeviceId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.ReceivedAt);
                entity.HasIndex(e => e.CreatedAt);

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

                entity.HasQueryFilter(e => !e.IsDeleted);
                entity.HasIndex(e => e.IsDeleted).HasFilter("\"IsDeleted\" = false");
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

                entity.HasIndex(e => e.EntityType);
                entity.HasIndex(e => e.Action);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => new { e.EntityType, e.EntityId });

                entity.HasQueryFilter(e => !e.IsDeleted);
                entity.HasIndex(e => e.IsDeleted).HasFilter("\"IsDeleted\" = false");
            });
            #endregion
        }
    }
}
