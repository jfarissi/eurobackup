using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Backup.Web.Api.Server.Models.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;
using Backup.Web.Api.Server.Models.Rols;
using Backup.Web.Api.Server.Models;
using Backup.Web.Api.Server.Models.Catalog;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Entities.Accounting;
using Backup.Web.Api.Server.Models.Entities.Email;
using Backup.Web.Api.Server.Models.Entities.SaaS;
using Backup.Web.Api.Server.Services.Audit;
using Backup.Web.Api.Server.Services.Sales;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial class StorageBroker : IdentityDbContext<User, Role, Guid>, IStorageBroker
    {
        private readonly IConfiguration configuration;
        private readonly IHttpContextAccessor? httpContextAccessor;

        /// <summary>Lit un DateTime? DB (NULL legacy) comme DateTime non-null.</summary>
        private static readonly ValueConverter<DateTime, DateTime?> NullableDateTimeAsUtcConverter =
            new(
                model => model == default ? null : model,
                provider => provider.HasValue
                    ? DateTime.SpecifyKind(provider.Value, DateTimeKind.Utc)
                    : DateTime.UnixEpoch);

        public StorageBroker(IConfiguration configuration, IHttpContextAccessor? httpContextAccessor = null)
        {
            this.configuration = configuration;
            this.httpContextAccessor = httpContextAccessor;
        }

        public override int SaveChanges()
        {
            var pendingCreated = this.ApplyAuditTrail();
            var result = base.SaveChanges();
            this.FlushPendingEntityAuditLogs(pendingCreated);
            return result;
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var pendingCreated = this.ApplyAuditTrail();
            var result = await base.SaveChangesAsync(cancellationToken);
            await this.FlushPendingEntityAuditLogsAsync(pendingCreated, cancellationToken);
            return result;
        }

        /// <summary>
        /// Stamp CreatedBy/UpdatedBy + file EntityAuditLog (Updated/Deleted immédiatement,
        /// Created après génération de la clé).
        /// </summary>
        private List<EntityEntry> ApplyAuditTrail()
        {
            var actor = SalesDocumentAudit.ActorFrom(this.httpContextAccessor?.HttpContext?.User);
            var now = DateTime.UtcNow;
            var companyFallback = this.httpContextAccessor?.HttpContext?.Request.Headers["X-Company-ID"].FirstOrDefault()
                ?? this.httpContextAccessor?.HttpContext?.User?.FindFirst("CompanyId")?.Value
                ?? this.httpContextAccessor?.HttpContext?.User?.FindFirst("companyId")?.Value;
            var pendingCreated = new List<EntityEntry>();

            foreach (var entry in this.ChangeTracker.Entries<IHasAuditTrail>().ToList())
            {
                if (entry.State == EntityState.Added)
                {
                    AuditTrail.StampCreate(entry.Entity, actor, now);
                    if (entry.Entity is Models.Document document && document.DateAdded == default)
                        document.DateAdded = now;
                    if (ShouldWriteEntityAudit(entry))
                        pendingCreated.Add(entry);
                }
                else if (entry.State == EntityState.Modified)
                {
                    AuditTrail.StampUpdate(entry.Entity, actor, now);
                    entry.Property(e => e.CreatedAt).IsModified = false;
                    entry.Property(e => e.CreatedBy).IsModified = false;
                    if (entry.Entity is Models.StockItem stock)
                        stock.LastUpdated = now;

                    if (ShouldWriteEntityAudit(entry) && HasMeaningfulPropertyChanges(entry))
                    {
                        this.EntityAuditLogs.Add(BuildEntityAuditLog(entry, "Updated", actor, now, companyFallback));
                    }
                }
                else if (entry.State == EntityState.Deleted)
                {
                    if (ShouldWriteEntityAudit(entry))
                        this.EntityAuditLogs.Add(BuildEntityAuditLog(entry, "Deleted", actor, now, companyFallback));
                }
            }

            return pendingCreated;
        }

        private void FlushPendingEntityAuditLogs(List<EntityEntry> pendingCreated)
        {
            if (pendingCreated == null || pendingCreated.Count == 0)
                return;

            AddPendingCreatedLogs(pendingCreated);
            base.SaveChanges();
        }

        private async Task FlushPendingEntityAuditLogsAsync(List<EntityEntry> pendingCreated, CancellationToken cancellationToken)
        {
            if (pendingCreated == null || pendingCreated.Count == 0)
                return;

            AddPendingCreatedLogs(pendingCreated);
            await base.SaveChangesAsync(cancellationToken);
        }

        private void AddPendingCreatedLogs(List<EntityEntry> pendingCreated)
        {
            var actor = SalesDocumentAudit.ActorFrom(this.httpContextAccessor?.HttpContext?.User);
            var now = DateTime.UtcNow;
            var companyFallback = this.httpContextAccessor?.HttpContext?.Request.Headers["X-Company-ID"].FirstOrDefault()
                ?? this.httpContextAccessor?.HttpContext?.User?.FindFirst("CompanyId")?.Value
                ?? this.httpContextAccessor?.HttpContext?.User?.FindFirst("companyId")?.Value;

            foreach (var entry in pendingCreated)
            {
                if (entry.Entity == null) continue;
                this.EntityAuditLogs.Add(BuildEntityAuditLog(entry, "Created", actor, now, companyFallback));
            }
        }

        private static bool ShouldWriteEntityAudit(EntityEntry entry)
        {
            var type = entry.Metadata.ClrType;
            if (type == typeof(EntityAuditLog) || type == typeof(DocumentAuditLog))
                return false;
            if (type == typeof(Models.ErpProductChangeLog))
                return false;

            var name = type.Name;
            // Lignes de documents : trop verbeux (déjà couvertes via l'en-tête).
            if (name.EndsWith("Line", StringComparison.Ordinal)
                || name.EndsWith("LineEntity", StringComparison.Ordinal)
                || name.EndsWith("Lines", StringComparison.Ordinal))
                return false;

            if (name is "DeliveryLineAdjustment" or "PaymentAllocation" or "UserCompany")
                return false;

            return true;
        }

        private static readonly HashSet<string> AuditOnlyProperties = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(IHasAuditTrail.CreatedAt),
            nameof(IHasAuditTrail.UpdatedAt),
            nameof(IHasAuditTrail.CreatedBy),
            nameof(IHasAuditTrail.UpdatedBy),
            "LastUpdated",
            "RowVersion"
        };

        private static bool HasMeaningfulPropertyChanges(EntityEntry entry)
        {
            foreach (var prop in entry.Properties)
            {
                if (!prop.IsModified) continue;
                if (AuditOnlyProperties.Contains(prop.Metadata.Name)) continue;
                return true;
            }
            return false;
        }

        private static EntityAuditLog BuildEntityAuditLog(
            EntityEntry entry,
            string action,
            string? actor,
            DateTime now,
            string? companyFallback)
        {
            var typeName = entry.Metadata.ClrType.Name;
            var key = FormatEntityKey(entry);
            string? companyId = null;
            if (entry.Entity is Services.Tenancy.IHasCompanyId hasCompany)
                companyId = hasCompany.CompanyId;
            if (string.IsNullOrWhiteSpace(companyId))
                companyId = companyFallback;

            string? details = null;
            if (action == "Updated")
            {
                var changed = entry.Properties
                    .Where(p => p.IsModified && !AuditOnlyProperties.Contains(p.Metadata.Name))
                    .Select(p => p.Metadata.Name)
                    .Take(40)
                    .ToList();
                if (changed.Count > 0)
                    details = string.Join(", ", changed);
            }

            var who = AuditTrail.NormalizeActor(actor);
            return new EntityAuditLog
            {
                EntityType = typeName,
                EntityKey = key,
                Action = action,
                Summary = $"{action} {typeName} #{key} by {who}",
                Details = details,
                Actor = who,
                CompanyId = companyId,
                CreatedAt = now
            };
        }

        private static string FormatEntityKey(EntityEntry entry)
        {
            var key = entry.Metadata.FindPrimaryKey();
            if (key == null || key.Properties.Count == 0)
                return "?";
            return string.Join(",", key.Properties.Select(p =>
                entry.Property(p.Name).CurrentValue?.ToString() ?? ""));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(u => u.CustomerId);
                entity.HasIndex(u => u.CustomerId);
            });

            modelBuilder.Entity<Document>(entity =>
            {
                entity.Property(d => d.TypeDocument).HasMaxLength(64);
                entity.Property(d => d.Numero).HasMaxLength(128);
                entity.Property(d => d.Client).HasMaxLength(256);
                entity.Property(d => d.Supplier).HasMaxLength(256);
                entity.Property(d => d.OriginalFileName).HasMaxLength(512);
                entity.Property(d => d.FilePath).HasMaxLength(1024);
            });
            modelBuilder.Entity<HelpContent>(entity =>
            {
                entity.HasIndex(h => new { h.HelpKey, h.Lang }).IsUnique();
                entity.Property(h => h.HelpKey).HasMaxLength(128);
                entity.Property(h => h.Lang).HasMaxLength(8);
                entity.Property(h => h.Title).HasMaxLength(256);
                entity.Property(h => h.N1).HasMaxLength(256);
                entity.Property(h => h.Version).HasMaxLength(32);
                entity.Property(h => h.Status).HasMaxLength(32);
            });
            modelBuilder.Entity<HelpFeedbackEvent>(entity =>
            {
                entity.HasIndex(e => new { e.HelpKey, e.CreatedAt });
                entity.Property(e => e.HelpKey).HasMaxLength(128);
                entity.Property(e => e.Vote).HasMaxLength(8);
            });
            modelBuilder.Entity<HelpAnalyticsEvent>(entity =>
            {
                entity.HasIndex(e => new { e.HelpKey, e.CreatedAt });
                entity.Property(e => e.HelpKey).HasMaxLength(128);
                entity.Property(e => e.Action).HasMaxLength(32);
            });
            modelBuilder.Entity<DocumentLine>(entity =>
            {
                entity.HasIndex(l => l.DocumentId);
                entity.Property(l => l.Product).HasMaxLength(1024);
                entity.Property(l => l.ProductCode).HasMaxLength(128);
				entity.Property(l => l.Ean).HasMaxLength(13);
                entity.Property(l => l.Unit).HasMaxLength(16);
				entity.Property(l => l.RawLine).HasMaxLength(2048);
            });
            modelBuilder.Entity<Backup.Web.Api.Server.Models.DocumentRelation>()
                .HasIndex(r => new { r.InvoiceId, r.DeliveryId })
                .IsUnique();
            modelBuilder.Entity<StockItem>(entity =>
            {
                entity.ToTable("Stock");
                entity.Property(s => s.CompanyId).HasMaxLength(36);
                entity.HasIndex(s => new { s.ProductKey, s.CompanyId }).IsUnique();
                entity.Property(s => s.ProductKey).HasMaxLength(256);
                entity.Property(s => s.Supplier).HasMaxLength(256);
                entity.Property(s => s.Description).HasMaxLength(1024);
                entity.Property(s => s.Unit).HasMaxLength(16);
                entity.Property(s => s.QuantityOnHand).HasPrecision(18, 4);
                entity.Property(s => s.ReservedQuantity).HasPrecision(18, 4);
                entity.Property(s => s.MinStock).HasPrecision(18, 4);
                entity.Property(s => s.AverageCost).HasPrecision(18, 4);
            });
            modelBuilder.Entity<StockUpdate>(entity =>
            {
                entity.HasIndex(s => s.DeliveryId);
                entity.HasIndex(s => s.ProductKey);
                entity.Property(s => s.ProductKey).HasMaxLength(256);
                entity.ToTable("StockUpdates");
            });
            modelBuilder.Entity<DeliveryLineAdjustment>(entity =>
            {
                entity.HasIndex(a => new { a.DeliveryId, a.ProductKey });
                entity.HasIndex(a => a.DeliveryId);
                entity.Property(a => a.ProductKey).HasMaxLength(256);
                entity.Property(a => a.CreatedBy).HasMaxLength(128);
                entity.Property(a => a.ValidatedBy).HasMaxLength(128);
            });

            modelBuilder.Entity<ErpProduct>(entity =>
            {
                entity.ToTable("ErpProducts");
                entity.HasKey(p => p.Id);
                entity.HasIndex(p => p.ErpProductId).IsUnique();
                entity.HasIndex(p => p.Ean);
                entity.HasIndex(p => p.Reference);
                entity.Property(p => p.ErpProductId).IsRequired().HasMaxLength(64);
                entity.Property(p => p.Name).HasMaxLength(512);
                entity.Property(p => p.Name2).HasMaxLength(512);
                entity.Property(p => p.Reference).HasMaxLength(128);
                entity.Property(p => p.Ean).HasMaxLength(64);
                entity.Property(p => p.Brand).HasMaxLength(256);
                entity.Property(p => p.Manufacturer).HasMaxLength(256);
                entity.Property(p => p.Model).HasMaxLength(256);
                entity.Property(p => p.Comment).HasMaxLength(2048);
                entity.Property(p => p.Link).HasMaxLength(1024);
                entity.Property(p => p.PicName).HasMaxLength(512);
                entity.Property(p => p.PerUnit).HasMaxLength(64);
                entity.Property(p => p.PieceID).HasMaxLength(64);
                entity.Property(p => p.MainTypeID).HasMaxLength(64);
                entity.Property(p => p.MainTypeName).HasMaxLength(256);
                entity.Property(p => p.MainSubTypeID).HasMaxLength(64);
                entity.Property(p => p.MainSubTypeName).HasMaxLength(256);
                entity.Property(p => p.TypeID).HasMaxLength(64);
                entity.Property(p => p.TypeName).HasMaxLength(256);
                entity.Property(p => p.SubTypeID).HasMaxLength(64);
                entity.Property(p => p.SubTypeName).HasMaxLength(256);
                entity.Property(p => p.SubProductID).HasMaxLength(64);
                entity.Property(p => p.Label).HasMaxLength(256);
                entity.Property(p => p.ColorCode).HasMaxLength(64);
                entity.Property(p => p.DataSource).HasMaxLength(32);
                entity.Property(p => p.SourceFile).HasMaxLength(512);
                entity.HasIndex(p => p.FromExcel);
                entity.HasIndex(p => p.IsDropship);
                entity.HasIndex(p => p.DropshipSupplierId);
                entity.Property(p => p.PriceHT).HasPrecision(18, 4);
                entity.Property(p => p.UnitPrice).HasPrecision(18, 4);
                entity.Property(p => p.CPrice).HasPrecision(18, 4);
                entity.Property(p => p.RPrice).HasPrecision(18, 4);
                entity.Property(p => p.TypeVatPerc).HasPrecision(18, 4);
                entity.Property(p => p.DiscountPerc).HasPrecision(18, 4);
                entity.Property(p => p.DiscountPrice).HasPrecision(18, 4);
                entity.Property(p => p.ProductDiscountPerc).HasPrecision(18, 4);
                entity.Property(p => p.TypeDiscountPerc).HasPrecision(18, 4);
                entity.Property(p => p.PromoPrice).HasPrecision(18, 4);
                entity.Property(p => p.StockQuantity).HasPrecision(18, 4);
                entity.Property(p => p.Quantity).HasPrecision(18, 4);
                entity.Property(p => p.Weight).HasPrecision(18, 4);
                entity.Property(p => p.Height).HasPrecision(18, 4);
                entity.Property(p => p.Width).HasPrecision(18, 4);
                entity.Property(p => p.Depth).HasPrecision(18, 4);

                entity.HasOne(p => p.BrandEntity)
                    .WithMany(b => b.Products)
                    .HasForeignKey(p => p.BrandId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(p => p.Category)
                    .WithMany(c => c.Products)
                    .HasForeignKey(p => p.CategoryId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(p => p.BrandId);
                entity.HasIndex(p => p.CategoryId);
            });

            modelBuilder.Entity<ErpProductVariant>(entity =>
            {
                entity.ToTable("ErpProductVariants");
                entity.HasKey(v => v.Id);
                entity.Property(v => v.Sku).IsRequired().HasMaxLength(100);
                entity.Property(v => v.Barcode).HasMaxLength(64);
                entity.Property(v => v.AttributesJson).HasMaxLength(8000);
                entity.Property(v => v.CreatedBy).HasMaxLength(128);
                entity.Property(v => v.UpdatedBy).HasMaxLength(128);
                entity.Property(v => v.CostPrice).HasPrecision(18, 4);
                entity.Property(v => v.PriceOverride).HasPrecision(18, 4);
                entity.Property(v => v.StockQuantity).HasPrecision(18, 4);
                entity.Property(v => v.Weight).HasPrecision(18, 4);
                entity.Property(v => v.Length).HasPrecision(18, 4);
                entity.Property(v => v.Width).HasPrecision(18, 4);
                entity.Property(v => v.Height).HasPrecision(18, 4);
                entity.HasIndex(v => v.Sku).IsUnique();
                entity.HasIndex(v => v.Barcode).IsUnique();
                entity.HasIndex(v => v.ProductId);
                entity.HasOne(v => v.Product)
                    .WithMany()
                    .HasForeignKey(v => v.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ErpProductImage>(entity =>
            {
                entity.ToTable("ErpProductImages");
                entity.HasKey(i => i.Id);
                entity.Property(i => i.Url).IsRequired().HasMaxLength(1024);
                entity.Property(i => i.AltText).HasMaxLength(255);
                entity.Property(i => i.CreatedBy).HasMaxLength(128);
                entity.Property(i => i.UpdatedBy).HasMaxLength(128);
                entity.HasIndex(i => i.ProductId);
                entity.HasIndex(i => new { i.ProductId, i.IsMain });
                entity.HasOne(i => i.Product)
                    .WithMany()
                    .HasForeignKey(i => i.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ErpProductDiagram>(entity =>
            {
                entity.ToTable("ErpProductDiagrams");
                entity.HasKey(d => d.Id);
                entity.Property(d => d.Title).IsRequired().HasMaxLength(255);
                entity.Property(d => d.ImageUrl).IsRequired().HasMaxLength(2048);
                entity.Property(d => d.MediaKind).IsRequired().HasMaxLength(16);
                entity.Property(d => d.Source).IsRequired().HasMaxLength(16);
                entity.Property(d => d.CreatedBy).HasMaxLength(128);
                entity.HasIndex(d => d.ProductId);
                entity.HasOne(d => d.Product)
                    .WithMany()
                    .HasForeignKey(d => d.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ErpDiagramHotspot>(entity =>
            {
                entity.ToTable("ErpDiagramHotspots");
                entity.HasKey(h => h.Id);
                entity.Property(h => h.Label).IsRequired().HasMaxLength(128);
                entity.Property(h => h.Shape).IsRequired().HasMaxLength(16);
                entity.Property(h => h.CoordsJson).IsRequired().HasMaxLength(2000);
                entity.HasIndex(h => h.DiagramId);
                entity.HasIndex(h => h.TargetProductId);
                entity.HasOne(h => h.Diagram)
                    .WithMany(d => d.Hotspots)
                    .HasForeignKey(h => h.DiagramId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(h => h.TargetProduct)
                    .WithMany()
                    .HasForeignKey(h => h.TargetProductId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ErpProductAttributeDefinition>(entity =>
            {
                entity.ToTable("ErpProductAttributeDefinitions");
                entity.HasKey(a => a.Id);
                entity.Property(a => a.CompanyId).IsRequired().HasMaxLength(36);
                entity.Property(a => a.Code).IsRequired().HasMaxLength(64);
                entity.Property(a => a.Name).IsRequired().HasMaxLength(128);
                entity.Property(a => a.CreatedBy).HasMaxLength(128);
                entity.Property(a => a.UpdatedBy).HasMaxLength(128);
                entity.HasIndex(a => new { a.CompanyId, a.Code }).IsUnique();
            });

            modelBuilder.Entity<ErpProductAttributeValue>(entity =>
            {
                entity.ToTable("ErpProductAttributeValues");
                entity.HasKey(v => v.Id);
                entity.Property(v => v.Value).IsRequired();
                entity.Property(v => v.CreatedBy).HasMaxLength(128);
                entity.Property(v => v.UpdatedBy).HasMaxLength(128);
                entity.HasIndex(v => v.ProductId);
                entity.HasIndex(v => new { v.ProductId, v.AttributeId }).IsUnique();
                entity.HasOne(v => v.Product)
                    .WithMany()
                    .HasForeignKey(v => v.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(v => v.Attribute)
                    .WithMany()
                    .HasForeignKey(v => v.AttributeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ErpProductVehicle>(entity =>
            {
                entity.ToTable("ErpProductVehicles");
                entity.HasKey(v => v.Id);
                entity.Property(v => v.Make).IsRequired().HasMaxLength(128);
                entity.Property(v => v.Model).IsRequired().HasMaxLength(128);
                entity.Property(v => v.TypeName).HasMaxLength(256);
                entity.Property(v => v.EngineCode).HasMaxLength(64);
                entity.Property(v => v.KType).HasMaxLength(64);
                entity.Property(v => v.ExternalManufacturerId).HasMaxLength(64);
                entity.Property(v => v.ExternalModelId).HasMaxLength(64);
                entity.Property(v => v.BodyType).HasMaxLength(64);
                entity.Property(v => v.FuelType).HasMaxLength(64);
                entity.Property(v => v.DriveType).HasMaxLength(64);
                entity.Property(v => v.Transmission).HasMaxLength(64);
                entity.HasIndex(v => v.ProductId);
                entity.HasIndex(v => new { v.Make, v.Model });
                entity.HasIndex(v => v.KType);
                entity.HasIndex(v => v.EngineCode);
                entity.HasIndex(v => v.FuelType);
                entity.HasIndex(v => v.BodyType);
                entity.HasIndex(v => v.DriveType);
                entity.HasIndex(v => v.Transmission);
                entity.HasOne(v => v.Product)
                    .WithMany()
                    .HasForeignKey(v => v.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ErpPlateHistory>(entity =>
            {
                entity.ToTable("ErpPlateHistories");
                entity.HasKey(h => h.Id);
                entity.Property(h => h.CompanyId).HasMaxLength(36);
                entity.Property(h => h.PlateNumber).IsRequired().HasMaxLength(32);
                entity.Property(h => h.Country).HasMaxLength(8);
                entity.Property(h => h.Vin).HasMaxLength(32);
                entity.Property(h => h.Make).HasMaxLength(128);
                entity.Property(h => h.Model).HasMaxLength(128);
                entity.Property(h => h.EngineCode).HasMaxLength(64);
                entity.Property(h => h.FuelType).HasMaxLength(64);
                entity.Property(h => h.SearchedBy).HasMaxLength(128);
                entity.HasIndex(h => new { h.CompanyId, h.SearchedAt });
                entity.HasIndex(h => h.PlateNumber);
            });

            modelBuilder.Entity<ErpPlateVehicle>(entity =>
            {
                entity.ToTable("ErpPlateVehicles");
                entity.HasKey(v => v.Id);
                entity.Property(v => v.CompanyId).HasMaxLength(36);
                entity.Property(v => v.PlateNumber).IsRequired().HasMaxLength(32);
                entity.Property(v => v.Country).IsRequired().HasMaxLength(8);
                entity.Property(v => v.Vin).HasMaxLength(32);
                entity.Property(v => v.KType).HasMaxLength(64);
                entity.Property(v => v.Make).HasMaxLength(128);
                entity.Property(v => v.Model).HasMaxLength(128);
                entity.Property(v => v.EngineCode).HasMaxLength(64);
                entity.Property(v => v.FuelType).HasMaxLength(64);
                entity.Property(v => v.Source).IsRequired().HasMaxLength(32);
                entity.HasIndex(v => v.CustomerId);
                entity.Property(v => v.CreatedBy).HasMaxLength(128);
                entity.Property(v => v.UpdatedBy).HasMaxLength(128);
                entity.HasIndex(v => new { v.CompanyId, v.PlateNumber, v.Country }).IsUnique();
                entity.HasIndex(v => v.Vin);
                entity.HasIndex(v => v.KType);
            });

            modelBuilder.Entity<ErpKTypeEnrichmentQueue>(entity =>
            {
                entity.ToTable("ErpKTypeEnrichmentQueue");
                entity.HasKey(q => q.Id);
                entity.Property(q => q.CompanyId).HasMaxLength(36);
                entity.Property(q => q.KType).IsRequired().HasMaxLength(64);
                entity.Property(q => q.Vin).HasMaxLength(32);
                entity.Property(q => q.Make).HasMaxLength(128);
                entity.Property(q => q.Model).HasMaxLength(128);
                entity.Property(q => q.EngineCode).HasMaxLength(64);
                entity.Property(q => q.Source).IsRequired().HasMaxLength(32);
                entity.Property(q => q.Status).IsRequired().HasMaxLength(16);
                entity.HasIndex(q => q.KType).IsUnique();
                entity.HasIndex(q => new { q.Status, q.HitCount });
                entity.HasIndex(q => q.CompanyId);
            });

            modelBuilder.Entity<ErpRapidApiKTypeCategoryCache>(entity =>
            {
                entity.ToTable("ErpRapidApiKTypeCategoryCache");
                entity.HasKey(c => c.Id);
                entity.Property(c => c.KType).IsRequired().HasMaxLength(64);
                entity.Property(c => c.CategoriesJson).IsRequired();
                entity.HasIndex(c => c.KType).IsUnique();
            });

            modelBuilder.Entity<ErpProductSupplierOffer>(entity =>
            {
                entity.ToTable("ErpProductSupplierOffers");
                entity.HasKey(o => o.Id);
                entity.Property(o => o.CompanyId).HasMaxLength(36);
                entity.Property(o => o.SupplierSku).HasMaxLength(128);
                entity.Property(o => o.Source).IsRequired().HasMaxLength(16);
                entity.Property(o => o.BuyPrice).HasPrecision(18, 4);
                entity.Property(o => o.StockQty).HasPrecision(18, 4);
                entity.HasIndex(o => new { o.CompanyId, o.ProductId, o.SupplierId }).IsUnique();
                entity.HasIndex(o => o.ProductId);
            });

            modelBuilder.Entity<ErpVinVehicle>(entity =>
            {
                entity.ToTable("ErpVinVehicles");
                entity.HasKey(v => v.Id);
                entity.Property(v => v.CompanyId).HasMaxLength(36);
                entity.Property(v => v.Vin).IsRequired().HasMaxLength(17);
                entity.Property(v => v.Make).HasMaxLength(128);
                entity.Property(v => v.Model).HasMaxLength(128);
                entity.Property(v => v.EngineCode).HasMaxLength(64);
                entity.Property(v => v.FuelType).HasMaxLength(64);
                entity.Property(v => v.ExternalVehicleId).HasMaxLength(64);
                entity.Property(v => v.ExternalModelId).HasMaxLength(64);
                entity.Property(v => v.ExternalManufacturerId).HasMaxLength(64);
                entity.Property(v => v.Source).IsRequired().HasMaxLength(32);
                entity.HasIndex(v => v.Vin).IsUnique();
                entity.HasIndex(v => new { v.Make, v.Model, v.Year });
                entity.HasIndex(v => v.CompanyId);
            });

            modelBuilder.Entity<ErpOemCrossReference>(entity =>
            {
                entity.ToTable("ErpOemCrossReferences");
                entity.HasKey(o => o.Id);
                entity.Property(o => o.OemNumber).IsRequired().HasMaxLength(128);
                entity.Property(o => o.Brand).HasMaxLength(128);
                entity.HasIndex(o => new { o.ProductId, o.OemNumber }).IsUnique();
                entity.HasIndex(o => o.OemNumber);
                entity.HasOne(o => o.Product)
                    .WithMany()
                    .HasForeignKey(o => o.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ErpBrand>(entity =>
            {
                entity.ToTable("ErpBrands");
                entity.HasKey(b => b.Id);
                entity.HasIndex(b => b.Name).IsUnique();
                entity.HasIndex(b => b.Slug).IsUnique();
                entity.Property(b => b.Name).IsRequired().HasMaxLength(255);
                entity.Property(b => b.Slug).IsRequired().HasMaxLength(255);
                entity.Property(b => b.LogoUrl).HasMaxLength(500);
                entity.Property(b => b.WebsiteUrl).HasMaxLength(500);
                entity.Property(b => b.Description).HasMaxLength(1000);
                // Lignes legacy avec CreatedAt/UpdatedAt NULL en base.
                // Note: IsRequired(false) interdit sur DateTime non-nullable ; le converter suffit.
                entity.Property(b => b.CreatedAt).HasConversion(NullableDateTimeAsUtcConverter);
                entity.Property(b => b.UpdatedAt).HasConversion(NullableDateTimeAsUtcConverter);
            });

            modelBuilder.Entity<ErpCategory>(entity =>
            {
                entity.ToTable("ErpCategories");
                entity.HasKey(c => c.Id);
                entity.HasIndex(c => new { c.Level, c.ErpExternalId }).IsUnique();
                entity.HasIndex(c => c.ParentId);
                entity.HasIndex(c => c.SlugNl);
                entity.Property(c => c.ErpExternalId).IsRequired().HasMaxLength(64);
                entity.Property(c => c.Level).IsRequired().HasMaxLength(32);
                entity.Property(c => c.NameNl).IsRequired().HasMaxLength(255);
                entity.Property(c => c.NameFr).IsRequired().HasMaxLength(255);
                entity.Property(c => c.NameEn).IsRequired().HasMaxLength(255);
                entity.Property(c => c.SlugNl).IsRequired().HasMaxLength(255);
                entity.Property(c => c.SlugFr).IsRequired().HasMaxLength(255);
                entity.Property(c => c.SlugEn).IsRequired().HasMaxLength(255);
                entity.Property(c => c.CreatedAt).HasConversion(NullableDateTimeAsUtcConverter);
                entity.Property(c => c.UpdatedAt).HasConversion(NullableDateTimeAsUtcConverter);
                entity.HasOne(c => c.Parent)
                    .WithMany(c => c.Children)
                    .HasForeignKey(c => c.ParentId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ErpProductChangeLog>(entity =>
            {
                entity.ToTable("ErpProductChangeLogs");
                entity.HasKey(c => c.Id);
                entity.HasIndex(c => c.ErpProductId);
                entity.HasIndex(c => c.DetectedAt);
                entity.HasIndex(c => c.IsRead);
                entity.HasIndex(c => c.SyncJobId);
                entity.Property(c => c.ChangeType).IsRequired().HasMaxLength(64);
                entity.Property(c => c.FieldName).IsRequired().HasMaxLength(128);
                entity.Property(c => c.OldValue).HasMaxLength(2048);
                entity.Property(c => c.NewValue).HasMaxLength(2048);
                entity.Property(c => c.SyncJobId).HasMaxLength(64);
                entity.HasOne(c => c.ErpProduct)
                    .WithMany(p => p.ChangeLogs)
                    .HasForeignKey(c => c.ErpProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ErpSyncLog>(entity =>
            {
                entity.ToTable("ErpSyncLogs");
                entity.HasKey(s => s.Id);
                entity.HasIndex(s => s.JobId).IsUnique();
                entity.HasIndex(s => s.StartedAt);
                entity.Property(s => s.JobId).IsRequired().HasMaxLength(64);
                entity.Property(s => s.Status).IsRequired().HasMaxLength(64);
                entity.Property(s => s.ErrorMessage).HasMaxLength(4000);
                entity.Property(s => s.Details).HasColumnType("longtext");
            });

            modelBuilder.Entity<StoreChatQuote>(entity =>
            {
                entity.ToTable("StoreChatQuotes");
                entity.HasKey(q => q.Id);
                entity.HasIndex(q => q.SessionId);
                entity.HasIndex(q => q.CreatedAt);
                entity.HasIndex(q => q.SalesProjectId);
                entity.Property(q => q.SessionId).IsRequired().HasMaxLength(64);
                entity.Property(q => q.Number).IsRequired().HasMaxLength(64);
                entity.Property(q => q.TotalAmount).HasPrecision(18, 4);
                entity.Property(q => q.FileName).HasMaxLength(256);
                entity.Property(q => q.PdfBase64).HasColumnType("longtext");
                entity.Property(q => q.LinesJson).HasColumnType("longtext");
            });

            modelBuilder.Entity<StoreChatOrder>(entity =>
            {
                entity.ToTable("StoreChatOrders");
                entity.HasKey(o => o.Id);
                entity.HasIndex(o => o.SessionId);
                entity.HasIndex(o => o.Status);
                entity.HasIndex(o => o.CreatedAt);
                entity.HasIndex(o => o.SalesProjectId);
                entity.Property(o => o.SessionId).IsRequired().HasMaxLength(64);
                entity.Property(o => o.Status).IsRequired().HasMaxLength(32);
                entity.Property(o => o.StripeSessionId).HasMaxLength(128);
                entity.Property(o => o.InvoiceNumber).HasMaxLength(64);
                entity.Property(o => o.TotalAmount).HasPrecision(18, 4);
                entity.Property(o => o.InvoiceFileName).HasMaxLength(256);
                entity.Property(o => o.InvoicePdfBase64).HasColumnType("longtext");
                entity.Property(o => o.LinesJson).HasColumnType("longtext");
            });

            modelBuilder.Entity<StoreChatTurn>(entity =>
            {
                entity.ToTable("StoreChatTurns");
                entity.HasKey(t => t.Id);
                entity.HasIndex(t => t.SessionId);
                entity.HasIndex(t => t.CreatedAt);
                entity.HasIndex(t => t.SalesProjectId);
                entity.HasIndex(t => t.ReviewStatus);
                entity.Property(t => t.SessionId).IsRequired().HasMaxLength(64);
                entity.Property(t => t.PreferredLanguage).HasMaxLength(8);
                entity.Property(t => t.DomainId).HasMaxLength(64);
                entity.Property(t => t.ClientIntent).HasMaxLength(64);
                entity.Property(t => t.ActionType).HasMaxLength(64);
                entity.Property(t => t.UserText).HasColumnType("longtext");
                entity.Property(t => t.ReplyText).HasColumnType("longtext");
                entity.Property(t => t.ProductsJson).HasColumnType("longtext");
                entity.Property(t => t.ReviewStatus).HasMaxLength(16);
                entity.Property(t => t.ReviewNote).HasMaxLength(2000);
                entity.Property(t => t.ReviewSource).HasMaxLength(16);
                entity.HasIndex(t => t.IsCorrected);
            });

            modelBuilder.Entity<SalesProject>(entity =>
            {
                entity.ToTable("SalesProjects");
                entity.HasKey(p => p.Id);
                entity.HasIndex(p => p.SessionId);
                entity.HasIndex(p => p.Status);
                entity.Property(p => p.SessionId).IsRequired().HasMaxLength(64);
                entity.Property(p => p.Status).IsRequired().HasMaxLength(32);
                entity.Property(p => p.ProjectType).IsRequired().HasMaxLength(64);
                entity.Property(p => p.Title).HasMaxLength(256);
                entity.Property(p => p.SurfaceM2).HasPrecision(18, 4);
                entity.Property(p => p.LengthM).HasPrecision(18, 4);
                entity.Property(p => p.HeightM).HasPrecision(18, 4);
                entity.Property(p => p.BudgetMax).HasPrecision(18, 4);
                entity.Property(p => p.PreferredBrand).HasMaxLength(128);
                entity.Property(p => p.PreferredCategoriesJson).HasMaxLength(1024);
                entity.Property(p => p.PreferredWeightKg).HasPrecision(18, 4);
                entity.Property(p => p.SkillLevel).HasMaxLength(32);
                entity.Property(p => p.Style).HasMaxLength(64);
                entity.Property(p => p.CustomerId).HasMaxLength(64);
                entity.Property(p => p.PlanningText).HasMaxLength(4000);
                entity.Property(p => p.Notes).HasMaxLength(2000);
            });

            modelBuilder.Entity<SalesProjectChecklistItem>(entity =>
            {
                entity.ToTable("SalesProjectChecklistItems");
                entity.HasKey(i => i.Id);
                entity.HasIndex(i => i.SalesProjectId);
                entity.Property(i => i.Code).IsRequired().HasMaxLength(64);
                entity.Property(i => i.Label).IsRequired().HasMaxLength(256);
                entity.Property(i => i.Status).IsRequired().HasMaxLength(32);
                entity.HasOne(i => i.SalesProject)
                    .WithMany(p => p.ChecklistItems)
                    .HasForeignKey(i => i.SalesProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SalesCustomerProfile>(entity =>
            {
                entity.ToTable("SalesCustomerProfiles");
                entity.HasKey(p => p.Id);
                entity.HasIndex(p => p.CustomerId).IsUnique();
                entity.Property(p => p.CustomerId).IsRequired().HasMaxLength(64);
                entity.Property(p => p.PreferredBrandsJson).HasMaxLength(1024);
                entity.Property(p => p.AverageBudget).HasPrecision(18, 4);
                entity.Property(p => p.Notes).HasMaxLength(2000);
            });

            modelBuilder.Entity<Customer>(entity =>
            {
                entity.ToTable("Customers");
                entity.HasKey(c => c.Id);
                entity.Property(c => c.CompanyId).HasMaxLength(36);
                entity.HasIndex(c => new { c.CustomerCode, c.CompanyId }).IsUnique();
                entity.Property(c => c.CustomerCode).IsRequired().HasMaxLength(64);
                entity.Property(c => c.Name).IsRequired().HasMaxLength(256);
                entity.Property(c => c.Balance).HasPrecision(18, 4);
                entity.Property(c => c.CreditLimit).HasPrecision(18, 4);
                entity.Property(c => c.PaymentTerms).HasMaxLength(128);
                entity.Property(c => c.Status).IsRequired().HasMaxLength(32);
            });

            modelBuilder.Entity<Supplier>(entity =>
            {
                entity.ToTable("Suppliers");
                entity.HasKey(s => s.Id);
                entity.Property(s => s.CompanyId).HasMaxLength(36);
                entity.HasIndex(s => new { s.SupplierCode, s.CompanyId }).IsUnique();
                entity.Property(s => s.SupplierCode).IsRequired().HasMaxLength(64);
                entity.Property(s => s.Name).IsRequired().HasMaxLength(256);
                entity.Property(s => s.Balance).HasPrecision(18, 4);
                entity.Property(s => s.Status).IsRequired().HasMaxLength(32);
                entity.Property(s => s.FeedCode).HasMaxLength(64);
            });

            modelBuilder.Entity<Quote>(entity =>
            {
                entity.ToTable("Quotes");
                entity.HasKey(q => q.Id);
                entity.Property(q => q.CompanyId).HasMaxLength(36);
                entity.HasIndex(q => new { q.QuoteNumber, q.CompanyId }).IsUnique();
                entity.Property(q => q.QuoteNumber).IsRequired().HasMaxLength(64);
                entity.Property(q => q.TotalHT).HasPrecision(18, 4);
                entity.Property(q => q.TotalVat).HasPrecision(18, 4);
                entity.Property(q => q.TotalTTC).HasPrecision(18, 4);
                entity.Property(q => q.HeaderDiscountPercent).HasPrecision(9, 4);
                entity.Property(q => q.CurrencyCode).HasMaxLength(8);
                entity.HasOne(q => q.Customer).WithMany().HasForeignKey(q => q.CustomerId);
            });

            modelBuilder.Entity<QuoteLine>(entity =>
            {
                entity.ToTable("QuoteLines");
                entity.HasKey(l => l.Id);
                entity.Property(l => l.UnitPrice).HasPrecision(18, 4);
                entity.Property(l => l.Quantity).HasPrecision(18, 4);
                entity.Property(l => l.ConvertedQuantity).HasPrecision(18, 4);
                entity.Property(l => l.DiscountPercent).HasPrecision(9, 4);
                entity.Property(l => l.VatRate).HasPrecision(18, 4);
                entity.Property(l => l.TotalHT).HasPrecision(18, 4);
                entity.Property(l => l.TotalTTC).HasPrecision(18, 4);
                entity.HasOne(l => l.Quote).WithMany(q => q.Lines).HasForeignKey(l => l.QuoteId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SalesOrder>(entity =>
            {
                entity.ToTable("SalesOrders");
                entity.HasKey(o => o.Id);
                entity.Property(o => o.CompanyId).HasMaxLength(36);
                entity.HasIndex(o => new { o.OrderNumber, o.CompanyId }).IsUnique();
                entity.Property(o => o.OrderNumber).IsRequired().HasMaxLength(64);
                entity.Property(o => o.TotalHT).HasPrecision(18, 4);
                entity.Property(o => o.TotalVat).HasPrecision(18, 4);
                entity.Property(o => o.TotalTTC).HasPrecision(18, 4);
                entity.Property(o => o.HeaderDiscountPercent).HasPrecision(9, 4);
                entity.Property(o => o.CurrencyCode).HasMaxLength(8);
                entity.Property(o => o.BillingAddress).HasMaxLength(512);
                entity.Property(o => o.ShippingAddress).HasMaxLength(512);
                entity.HasOne(o => o.Customer).WithMany().HasForeignKey(o => o.CustomerId);
            });

            modelBuilder.Entity<SalesOrderLine>(entity =>
            {
                entity.ToTable("SalesOrderLines");
                entity.HasKey(l => l.Id);
                entity.Property(l => l.UnitPrice).HasPrecision(18, 4);
                entity.Property(l => l.Quantity).HasPrecision(18, 4);
                entity.Property(l => l.DeliveredQuantity).HasPrecision(18, 4);
                entity.Property(l => l.InvoicedQuantity).HasPrecision(18, 4);
                entity.Property(l => l.ReservedQuantity).HasPrecision(18, 4);
                entity.Property(l => l.DiscountPercent).HasPrecision(9, 4);
                entity.Property(l => l.VatRate).HasPrecision(18, 4);
                entity.Property(l => l.TotalHT).HasPrecision(18, 4);
                entity.Property(l => l.TotalTTC).HasPrecision(18, 4);
                entity.HasOne(l => l.SalesOrder).WithMany(o => o.Lines).HasForeignKey(l => l.SalesOrderId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SalesInvoice>(entity =>
            {
                entity.ToTable("SalesInvoices");
                entity.HasKey(i => i.Id);
                entity.Property(i => i.CompanyId).HasMaxLength(36);
                entity.HasIndex(i => new { i.InvoiceNumber, i.CompanyId }).IsUnique();
                entity.Property(i => i.InvoiceNumber).IsRequired().HasMaxLength(64);
                entity.Property(i => i.TotalHT).HasPrecision(18, 4);
                entity.Property(i => i.TotalVat).HasPrecision(18, 4);
                entity.Property(i => i.TotalTTC).HasPrecision(18, 4);
                entity.Property(i => i.PaidAmount).HasPrecision(18, 4);
                entity.Property(i => i.HeaderDiscountPercent).HasPrecision(9, 4);
                entity.Property(i => i.CurrencyCode).HasMaxLength(8);
                entity.HasOne(i => i.Customer).WithMany().HasForeignKey(i => i.CustomerId);
            });

            modelBuilder.Entity<SalesInvoiceLine>(entity =>
            {
                entity.ToTable("SalesInvoiceLines");
                entity.HasKey(l => l.Id);
                entity.Property(l => l.UnitPrice).HasPrecision(18, 4);
                entity.Property(l => l.Quantity).HasPrecision(18, 4);
                entity.Property(l => l.OrderedQuantity).HasPrecision(18, 4);
                entity.Property(l => l.DeliveredQuantity).HasPrecision(18, 4);
                entity.Property(l => l.DiscountPercent).HasPrecision(9, 4);
                entity.Property(l => l.VatRate).HasPrecision(18, 4);
                entity.Property(l => l.TotalHT).HasPrecision(18, 4);
                entity.Property(l => l.TotalTTC).HasPrecision(18, 4);
                entity.Property(l => l.LotNumber).HasMaxLength(64);
                entity.HasOne(l => l.SalesInvoice).WithMany(i => i.Lines).HasForeignKey(l => l.SalesInvoiceId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CreditNoteEntity>(entity =>
            {
                entity.ToTable("CreditNotes");
                entity.HasKey(c => c.Id);
                entity.Property(c => c.CompanyId).HasMaxLength(36);
                entity.HasIndex(c => new { c.CreditNoteNumber, c.CompanyId }).IsUnique();
                entity.Property(c => c.CreditNoteNumber).IsRequired().HasMaxLength(64);
                entity.Property(c => c.TotalHT).HasPrecision(18, 4);
                entity.Property(c => c.TotalVat).HasPrecision(18, 4);
                entity.Property(c => c.TotalTTC).HasPrecision(18, 4);
                entity.Property(c => c.CurrencyCode).HasMaxLength(8);
                entity.HasOne(c => c.Customer).WithMany().HasForeignKey(c => c.CustomerId);
                // RG-AC4 : retour physique (BRC) éventuellement à l'origine de l'avoir.
                entity.HasOne(c => c.SalesReturn).WithMany().HasForeignKey(c => c.SalesReturnId).OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<CreditNoteLineEntity>(entity =>
            {
                entity.ToTable("CreditNoteLines");
                entity.HasKey(l => l.Id);
                entity.Property(l => l.UnitPrice).HasPrecision(18, 4);
                entity.Property(l => l.Quantity).HasPrecision(18, 4);
                entity.Property(l => l.VatRate).HasPrecision(18, 4);
                entity.Property(l => l.TotalHT).HasPrecision(18, 4);
                entity.Property(l => l.TotalTTC).HasPrecision(18, 4);
                entity.HasOne(l => l.CreditNote).WithMany(c => c.Lines).HasForeignKey(l => l.CreditNoteEntityId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<PurchaseOrder>(entity =>
            {
                entity.ToTable("PurchaseOrders");
                entity.HasKey(p => p.Id);
                entity.Property(p => p.CompanyId).HasMaxLength(36);
                entity.HasIndex(p => new { p.OrderNumber, p.CompanyId }).IsUnique();
                entity.Property(p => p.OrderNumber).IsRequired().HasMaxLength(64);
                entity.Property(p => p.TotalHT).HasPrecision(18, 4);
                entity.Property(p => p.TotalVat).HasPrecision(18, 4);
                entity.Property(p => p.TotalTTC).HasPrecision(18, 4);
                entity.Property(p => p.CurrencyCode).HasMaxLength(8);
                entity.HasOne(p => p.Supplier).WithMany().HasForeignKey(p => p.SupplierId);
                entity.HasIndex(p => p.SalesOrderId);
                entity.HasMany(p => p.SupplierInvoices)
                    .WithOne(i => i.PurchaseOrder)
                    .HasForeignKey(i => i.PurchaseOrderId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<PurchaseOrderLine>(entity =>
            {
                entity.ToTable("PurchaseOrderLines");
                entity.HasKey(l => l.Id);
                entity.Property(l => l.UnitPrice).HasPrecision(18, 4);
                entity.Property(l => l.Quantity).HasPrecision(18, 4);
                entity.Property(l => l.ReceivedQuantity).HasPrecision(18, 4);
                entity.Property(l => l.InvoicedQuantity).HasPrecision(18, 4);
                entity.Property(l => l.VatRate).HasPrecision(18, 4);
                entity.Property(l => l.TotalHT).HasPrecision(18, 4);
                entity.Property(l => l.TotalTTC).HasPrecision(18, 4);
                entity.HasOne(l => l.PurchaseOrder).WithMany(p => p.Lines).HasForeignKey(l => l.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SupplierInvoiceEntity>(entity =>
            {
                entity.ToTable("SupplierInvoices");
                entity.HasKey(s => s.Id);
                entity.Property(s => s.CompanyId).HasMaxLength(36);
                entity.HasIndex(s => new { s.InvoiceNumber, s.CompanyId }).IsUnique();
                entity.Property(s => s.InvoiceNumber).IsRequired().HasMaxLength(64);
                entity.Property(s => s.TotalHT).HasPrecision(18, 4);
                entity.Property(s => s.TotalVat).HasPrecision(18, 4);
                entity.Property(s => s.TotalTTC).HasPrecision(18, 4);
                entity.Property(s => s.CurrencyCode).HasMaxLength(8);
                entity.HasOne(s => s.Supplier).WithMany().HasForeignKey(s => s.SupplierId);
                entity.HasIndex(s => s.PurchaseOrderId);
                entity.HasIndex(s => s.ReceiptId);
                entity.HasOne(s => s.Receipt).WithMany().HasForeignKey(s => s.ReceiptId).OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<SupplierPayment>(entity =>
            {
                entity.ToTable("SupplierPayments");
                entity.HasKey(p => p.Id);
                entity.Property(p => p.CompanyId).HasMaxLength(36);
                entity.Property(p => p.Amount).HasPrecision(18, 4);
                entity.Property(p => p.Method).HasMaxLength(64);
                entity.Property(p => p.Reference).HasMaxLength(128);
                entity.Property(p => p.Status).HasMaxLength(32);
                entity.HasIndex(p => p.SupplierInvoiceId);
                entity.HasIndex(p => p.CompanyId);
                entity.HasOne(p => p.SupplierInvoice).WithMany().HasForeignKey(p => p.SupplierInvoiceId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SupplierInvoiceLineEntity>(entity =>
            {
                entity.ToTable("SupplierInvoiceLines");
                entity.HasKey(l => l.Id);
                entity.Property(l => l.UnitPrice).HasPrecision(18, 4);
                entity.Property(l => l.Quantity).HasPrecision(18, 4);
                entity.Property(l => l.VatRate).HasPrecision(18, 4);
                entity.Property(l => l.TotalHT).HasPrecision(18, 4);
                entity.Property(l => l.TotalTTC).HasPrecision(18, 4);
                entity.HasOne(l => l.SupplierInvoice).WithMany(s => s.Lines).HasForeignKey(l => l.SupplierInvoiceEntityId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<StockMovement>(entity =>
            {
                entity.ToTable("StockMovements");
                entity.HasKey(m => m.Id);
                entity.Property(m => m.CompanyId).HasMaxLength(36);
                entity.HasIndex(m => m.ProductKey);
                entity.Property(m => m.ProductKey).IsRequired().HasMaxLength(256);
                entity.Property(m => m.Quantity).HasPrecision(18, 4);
                entity.Property(m => m.UnitCost).HasPrecision(18, 4);
                entity.Property(m => m.StockValue).HasPrecision(18, 4);
                entity.Property(m => m.CreatedAt).HasConversion(NullableDateTimeAsUtcConverter);
                entity.Property(m => m.UpdatedAt).HasConversion(NullableDateTimeAsUtcConverter);
            });

            modelBuilder.Entity<DocumentNumberSequence>(entity =>
            {
                entity.ToTable("DocumentNumberSequences");
                entity.HasKey(s => s.Id);
                entity.HasIndex(s => new { s.DocumentType, s.CompanyId }).IsUnique();
                entity.Property(s => s.DocumentType).IsRequired().HasMaxLength(64);
                entity.Property(s => s.Prefix).HasMaxLength(16);
                entity.Property(s => s.FormatPattern).HasMaxLength(128);
            });

            modelBuilder.Entity<CashSession>(entity =>
            {
                entity.ToTable("CashSessions");
                entity.HasKey(c => c.Id);
                entity.Property(c => c.CompanyId).HasMaxLength(36);
                entity.HasIndex(c => new { c.SessionNumber, c.CompanyId }).IsUnique();
                entity.Property(c => c.OpeningBalance).HasPrecision(18, 4);
                entity.Property(c => c.ClosingBalance).HasPrecision(18, 4);
                entity.Property(c => c.ExpectedClosingBalance).HasPrecision(18, 4);
            });

            modelBuilder.Entity<CashOperation>(entity =>
            {
                entity.ToTable("CashOperations");
                entity.HasKey(o => o.Id);
                entity.Property(o => o.Amount).HasPrecision(18, 4);
                entity.HasOne(o => o.CashSession).WithMany(c => c.Operations).HasForeignKey(o => o.CashSessionId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Payment>(entity =>
            {
                entity.ToTable("Payments");
                entity.HasKey(p => p.Id);
                entity.Property(p => p.CompanyId).HasMaxLength(36);
                entity.Property(p => p.Method).HasMaxLength(50);
                entity.Property(p => p.Reference).HasMaxLength(100);
                entity.Property(p => p.Bank).HasMaxLength(100);
                entity.Property(p => p.Status).IsRequired().HasMaxLength(20);
                entity.Property(p => p.TerminalTransactionId).HasMaxLength(100);
                entity.Property(p => p.CreatedBy).HasMaxLength(128);
                entity.Property(p => p.Amount).HasPrecision(18, 4);
                entity.Property(p => p.RoundingDifference).HasPrecision(18, 4);
                entity.Property(p => p.ReceivedAmount).HasPrecision(18, 4);
                entity.Property(p => p.ChangeAmount).HasPrecision(18, 4);
                entity.HasIndex(p => new { p.CompanyId, p.SalesInvoiceId });
                entity.HasIndex(p => p.PaidAt);
                entity.HasOne(p => p.SalesInvoice).WithMany().HasForeignKey(p => p.SalesInvoiceId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Receipt>(entity =>
            {
                entity.ToTable("ErpReceipts");
                entity.HasKey(r => r.Id);
                entity.Property(r => r.CompanyId).HasMaxLength(36);
                entity.HasIndex(r => new { r.ReceiptNumber, r.CompanyId }).IsUnique();
                entity.HasIndex(r => r.DocumentId).IsUnique();
                entity.Property(r => r.ReceiptNumber).IsRequired().HasMaxLength(64);
                entity.Property(r => r.Status).HasMaxLength(32);
                entity.HasOne(r => r.Supplier).WithMany().HasForeignKey(r => r.SupplierId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(r => r.PurchaseOrder).WithMany().HasForeignKey(r => r.PurchaseOrderId).OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<ReceiptLine>(entity =>
            {
                entity.ToTable("ErpReceiptLines");
                entity.HasKey(l => l.Id);
                entity.Property(l => l.ProductKey).HasMaxLength(256);
                entity.Property(l => l.QuantityReceived).HasPrecision(18, 4);
                entity.Property(l => l.UnitPriceExclTax).HasPrecision(18, 4);
                entity.Property(l => l.TaxRatePercent).HasPrecision(18, 4);
                entity.Property(l => l.LineAmountExclTax).HasPrecision(18, 4);
                entity.Property(l => l.LineTaxAmount).HasPrecision(18, 4);
                entity.HasOne(l => l.Receipt).WithMany(r => r.Lines).HasForeignKey(l => l.ReceiptId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Tenant>(entity =>
            {
                entity.ToTable("Tenants");
                entity.HasKey(t => t.Id);
                entity.Property(t => t.Id).HasMaxLength(36);
                entity.Property(t => t.Name).HasMaxLength(256);
            });

            modelBuilder.Entity<Company>(entity =>
            {
                entity.ToTable("Companies");
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Id).HasMaxLength(36);
                entity.Property(c => c.TenantId).HasMaxLength(36);
                entity.Property(c => c.Name).HasMaxLength(256);
                entity.Property(c => c.DefaultLanguageCode).HasMaxLength(16);
                entity.Property(c => c.DefaultCurrencyCode).HasMaxLength(8);
                entity.Property(c => c.PublicDomain).HasMaxLength(256);
                entity.HasOne(c => c.Tenant).WithMany(t => t.Companies).HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CompanyModule>(entity =>
            {
                entity.ToTable("CompanyModules");
                entity.HasKey(m => m.Id);
                entity.Property(m => m.Id).HasMaxLength(36);
                entity.Property(m => m.CompanyId).HasMaxLength(36);
                entity.Property(m => m.ModuleCode).HasMaxLength(64);
                entity.Property(m => m.ModuleName).HasMaxLength(128);
                entity.HasIndex(m => new { m.CompanyId, m.ModuleCode }).IsUnique();
                entity.HasIndex(m => m.CompanyId);
                entity.HasIndex(m => m.ModuleCode);
                entity.HasOne(m => m.Company).WithMany().HasForeignKey(m => m.CompanyId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UserCompany>(entity =>
            {
                entity.ToTable("UserCompanies");
                entity.HasKey(uc => new { uc.UserId, uc.CompanyId });
                entity.Property(uc => uc.CompanyId).HasMaxLength(36);
                entity.HasOne(uc => uc.Company).WithMany(c => c.UserCompanies).HasForeignKey(uc => uc.CompanyId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SalesDeliveryNote>(entity =>
            {
                entity.ToTable("SalesDeliveryNotes");
                entity.HasKey(n => n.Id);
                entity.Property(n => n.CompanyId).HasMaxLength(36);
                entity.HasIndex(n => new { n.DeliveryNumber, n.CompanyId }).IsUnique();
                entity.Property(n => n.DeliveryNumber).IsRequired().HasMaxLength(64);
                entity.Property(n => n.TotalHT).HasPrecision(18, 4);
                entity.Property(n => n.TotalVat).HasPrecision(18, 4);
                entity.Property(n => n.TotalTTC).HasPrecision(18, 4);
                entity.HasOne(n => n.Customer).WithMany().HasForeignKey(n => n.CustomerId);
                entity.HasOne(n => n.SalesOrder).WithMany().HasForeignKey(n => n.SalesOrderId).IsRequired(false);
                entity.HasOne(n => n.SalesInvoice).WithMany().HasForeignKey(n => n.SalesInvoiceId).IsRequired(false);
            });

            modelBuilder.Entity<SalesDeliveryNoteLine>(entity =>
            {
                entity.ToTable("SalesDeliveryNoteLines");
                entity.HasKey(l => l.Id);
                entity.Property(l => l.UnitPrice).HasPrecision(18, 4);
                entity.Property(l => l.OrderedQuantity).HasPrecision(18, 4);
                entity.Property(l => l.DeliveredQuantity).HasPrecision(18, 4);
                entity.Property(l => l.VatRate).HasPrecision(18, 4);
                entity.Property(l => l.TotalHT).HasPrecision(18, 4);
                entity.Property(l => l.TotalTTC).HasPrecision(18, 4);
                entity.HasOne(l => l.SalesDeliveryNote).WithMany(n => n.Lines).HasForeignKey(l => l.SalesDeliveryNoteId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AccountingEntry>(entity =>
            {
                entity.ToTable("AccountingEntries");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CompanyId).HasMaxLength(36);
                entity.Property(e => e.EntryNumber).IsRequired().HasMaxLength(64);
                entity.Property(e => e.JournalType).HasMaxLength(64);
                entity.Property(e => e.ReferenceType).HasMaxLength(64);
                entity.Property(e => e.Status).HasMaxLength(32);
                entity.HasIndex(e => new { e.EntryNumber, e.CompanyId }).IsUnique();
                entity.HasIndex(e => new { e.ReferenceType, e.ReferenceId, e.CompanyId });
                entity.HasOne(e => e.Journal).WithMany().HasForeignKey(e => e.JournalId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.FiscalPeriod).WithMany().HasForeignKey(e => e.FiscalPeriodId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<AccountingEntryLine>(entity =>
            {
                entity.ToTable("AccountingEntryLines");
                entity.HasKey(l => l.Id);
                entity.Property(l => l.AccountCode).IsRequired().HasMaxLength(32);
                entity.Property(l => l.AccountLabel).IsRequired().HasMaxLength(256);
                entity.Property(l => l.Debit).HasPrecision(18, 4);
                entity.Property(l => l.Credit).HasPrecision(18, 4);
                entity.Property(l => l.LettrageCode).HasMaxLength(64);
                entity.HasOne(l => l.AccountingEntry).WithMany(e => e.Lines).HasForeignKey(l => l.AccountingEntryId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(l => l.ChartOfAccount).WithMany().HasForeignKey(l => l.ChartOfAccountId).OnDelete(DeleteBehavior.Restrict);
            });

            // Phase 1 — socle comptable : plan comptable, journaux, exercices/périodes, paramètres.
            modelBuilder.Entity<ChartOfAccount>(entity =>
            {
                entity.ToTable("ChartOfAccounts");
                entity.HasKey(a => a.Id);
                entity.Property(a => a.CompanyId).HasMaxLength(36);
                entity.Property(a => a.AccountNumber).IsRequired().HasMaxLength(32);
                entity.Property(a => a.Label).IsRequired().HasMaxLength(256);
                entity.Property(a => a.LabelArabic).HasMaxLength(256);
                entity.Property(a => a.AccountType).IsRequired().HasMaxLength(32);
                entity.HasIndex(a => new { a.CompanyId, a.AccountNumber }).IsUnique();
                entity.HasOne(a => a.Parent).WithMany().HasForeignKey(a => a.ParentId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Journal>(entity =>
            {
                entity.ToTable("Journals");
                entity.HasKey(j => j.Id);
                entity.Property(j => j.CompanyId).HasMaxLength(36);
                entity.Property(j => j.Code).IsRequired().HasMaxLength(16);
                entity.Property(j => j.Label).IsRequired().HasMaxLength(256);
                entity.Property(j => j.CounterpartAccountCode).HasMaxLength(32);
                entity.HasIndex(j => new { j.CompanyId, j.Code }).IsUnique();
            });

            modelBuilder.Entity<FiscalYear>(entity =>
            {
                entity.ToTable("FiscalYears");
                entity.HasKey(f => f.Id);
                entity.Property(f => f.CompanyId).HasMaxLength(36);
                entity.Property(f => f.Name).IsRequired().HasMaxLength(128);
                entity.Property(f => f.Status).IsRequired().HasMaxLength(32);
                entity.HasIndex(f => new { f.CompanyId, f.StartDate, f.EndDate });
            });

            modelBuilder.Entity<FiscalPeriod>(entity =>
            {
                entity.ToTable("FiscalPeriods");
                entity.HasKey(p => p.Id);
                entity.Property(p => p.CompanyId).HasMaxLength(36);
                entity.HasIndex(p => new { p.FiscalYearId, p.Year, p.Month }).IsUnique();
                entity.HasOne(p => p.FiscalYear).WithMany(f => f.Periods).HasForeignKey(p => p.FiscalYearId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CompanyAccountingSettings>(entity =>
            {
                entity.ToTable("CompanyAccountingSettings");
                entity.HasKey(s => s.Id);
                entity.Property(s => s.CompanyId).IsRequired().HasMaxLength(36);
                entity.HasIndex(s => s.CompanyId).IsUnique();
                entity.Property(s => s.PlanType).IsRequired().HasMaxLength(32);
                entity.Property(s => s.CustomerAccountCode).IsRequired().HasMaxLength(32);
                entity.Property(s => s.SupplierAccountCode).IsRequired().HasMaxLength(32);
                entity.Property(s => s.SalesAccountCode).IsRequired().HasMaxLength(32);
                entity.Property(s => s.PurchaseAccountCode).IsRequired().HasMaxLength(32);
                entity.Property(s => s.VatCollectedAccountCode).IsRequired().HasMaxLength(32);
                entity.Property(s => s.VatDeductibleAccountCode).IsRequired().HasMaxLength(32);
                entity.Property(s => s.BankAccountCode).IsRequired().HasMaxLength(32);
                entity.Property(s => s.CashAccountCode).IsRequired().HasMaxLength(32);
                entity.Property(s => s.CustomerDepositAccountCode).IsRequired().HasMaxLength(32);
            });

            // Phase 2 — mapping TVA par taux (comptes collecté/déductible spécifiques par société).
            modelBuilder.Entity<CompanyVatRateAccount>(entity =>
            {
                entity.ToTable("CompanyVatRateAccounts");
                entity.HasKey(v => v.Id);
                entity.Property(v => v.CompanyId).HasMaxLength(36);
                entity.Property(v => v.Rate).HasPrecision(18, 4);
                entity.Property(v => v.CollectedAccountCode).IsRequired().HasMaxLength(32);
                entity.Property(v => v.DeductibleAccountCode).IsRequired().HasMaxLength(32);
                entity.HasIndex(v => new { v.CompanyId, v.Rate }).IsUnique();
            });

            modelBuilder.Entity<VatDeclaration>(entity =>
            {
                entity.ToTable("VatDeclarations");
                entity.HasKey(d => d.Id);
                entity.Property(d => d.CompanyId).HasMaxLength(36);
                entity.Property(d => d.Status).IsRequired().HasMaxLength(32);
                entity.Property(d => d.DeclaredBy).HasMaxLength(128);
                entity.Property(d => d.TotalCollected).HasPrecision(18, 4);
                entity.Property(d => d.TotalDeductible).HasPrecision(18, 4);
                entity.Property(d => d.PreviousCredit).HasPrecision(18, 4);
                entity.Property(d => d.NetToPay).HasPrecision(18, 4);
                entity.HasIndex(d => new { d.CompanyId, d.Year, d.Month }).IsUnique();
                entity.HasOne(d => d.FiscalPeriod).WithMany().HasForeignKey(d => d.FiscalPeriodId).OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<VatDeclarationLine>(entity =>
            {
                entity.ToTable("VatDeclarationLines");
                entity.HasKey(l => l.Id);
                entity.Property(l => l.Rate).HasPrecision(18, 4);
                entity.Property(l => l.CollectedBase).HasPrecision(18, 4);
                entity.Property(l => l.CollectedVat).HasPrecision(18, 4);
                entity.Property(l => l.DeductibleBase).HasPrecision(18, 4);
                entity.Property(l => l.DeductibleVat).HasPrecision(18, 4);
                entity.HasOne(l => l.VatDeclaration).WithMany(d => d.Lines).HasForeignKey(l => l.VatDeclarationId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<BankReconciliation>(entity =>
            {
                entity.ToTable("BankReconciliations");
                entity.HasKey(r => r.Id);
                entity.Property(r => r.CompanyId).HasMaxLength(36);
                entity.Property(r => r.AccountCode).IsRequired().HasMaxLength(32);
                entity.Property(r => r.FileName).HasMaxLength(256);
                entity.Property(r => r.Status).IsRequired().HasMaxLength(32);
                entity.Property(r => r.CompletedBy).HasMaxLength(128);
                entity.Property(r => r.CreatedBy).HasMaxLength(128);
                entity.Property(r => r.UpdatedBy).HasMaxLength(128);
                entity.Property(r => r.StatementBalance).HasPrecision(18, 4);
                entity.Property(r => r.BookBalance).HasPrecision(18, 4);
                entity.HasIndex(r => new { r.CompanyId, r.StatementDate });
            });

            modelBuilder.Entity<BankStatementLine>(entity =>
            {
                entity.ToTable("BankStatementLines");
                entity.HasKey(l => l.Id);
                entity.Property(l => l.Label).IsRequired().HasMaxLength(512);
                entity.Property(l => l.Reference).HasMaxLength(128);
                entity.Property(l => l.MatchMethod).HasMaxLength(32);
                entity.Property(l => l.Debit).HasPrecision(18, 4);
                entity.Property(l => l.Credit).HasPrecision(18, 4);
                entity.Property(l => l.RunningBalance).HasPrecision(18, 4);
                entity.HasOne(l => l.BankReconciliation).WithMany(r => r.Lines).HasForeignKey(l => l.BankReconciliationId).OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(l => l.AccountingEntryLineId);
            });

            modelBuilder.Entity<FixedAsset>(entity =>
            {
                entity.ToTable("FixedAssets");
                entity.HasKey(a => a.Id);
                entity.Property(a => a.CompanyId).HasMaxLength(36);
                entity.Property(a => a.Code).IsRequired().HasMaxLength(32);
                entity.Property(a => a.Designation).IsRequired().HasMaxLength(256);
                entity.Property(a => a.AssetAccountCode).IsRequired().HasMaxLength(32);
                entity.Property(a => a.DepreciationAccountCode).IsRequired().HasMaxLength(32);
                entity.Property(a => a.ExpenseAccountCode).IsRequired().HasMaxLength(32);
                entity.Property(a => a.Mode).IsRequired().HasMaxLength(32);
                entity.Property(a => a.OriginValue).HasPrecision(18, 4);
                entity.Property(a => a.ResidualValue).HasPrecision(18, 4);
                entity.Property(a => a.DecliningRate).HasPrecision(18, 8);
                entity.Property(a => a.AccumulatedDepreciation).HasPrecision(18, 4);
                entity.Property(a => a.DisposalPrice).HasPrecision(18, 4);
                entity.HasIndex(a => new { a.CompanyId, a.Code }).IsUnique();
            });

            modelBuilder.Entity<DepreciationScheduleLine>(entity =>
            {
                entity.ToTable("DepreciationScheduleLines");
                entity.HasKey(l => l.Id);
                entity.Property(l => l.Charge).HasPrecision(18, 4);
                entity.Property(l => l.Accumulated).HasPrecision(18, 4);
                entity.Property(l => l.NetBookValue).HasPrecision(18, 4);
                entity.HasOne(l => l.FixedAsset).WithMany(a => a.Schedule).HasForeignKey(l => l.FixedAssetId).OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(l => new { l.FixedAssetId, l.Year, l.Month }).IsUnique();
            });

            modelBuilder.Entity<Employee>(entity =>
            {
                entity.ToTable("Employees");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CompanyId).HasMaxLength(36);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(128);
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(128);
                entity.Property(e => e.CnssNumber).HasMaxLength(32);
                entity.Property(e => e.BaseSalary).HasPrecision(18, 4);
                entity.Property(e => e.Overtime).HasPrecision(18, 4);
                entity.Property(e => e.Bonuses).HasPrecision(18, 4);
                entity.Property(e => e.BenefitsInKind).HasPrecision(18, 4);
                entity.HasIndex(e => e.CompanyId);
            });

            modelBuilder.Entity<Payslip>(entity =>
            {
                entity.ToTable("Payslips");
                entity.HasKey(p => p.Id);
                entity.Property(p => p.CompanyId).HasMaxLength(36);
                entity.Property(p => p.BaseSalary).HasPrecision(18, 4);
                entity.Property(p => p.Overtime).HasPrecision(18, 4);
                entity.Property(p => p.Bonuses).HasPrecision(18, 4);
                entity.Property(p => p.BenefitsInKind).HasPrecision(18, 4);
                entity.Property(p => p.Gross).HasPrecision(18, 4);
                entity.Property(p => p.CnssEmployee).HasPrecision(18, 4);
                entity.Property(p => p.CnssEmployer).HasPrecision(18, 4);
                entity.Property(p => p.AmoEmployee).HasPrecision(18, 4);
                entity.Property(p => p.AmoEmployer).HasPrecision(18, 4);
                entity.Property(p => p.Igr).HasPrecision(18, 4);
                entity.Property(p => p.Net).HasPrecision(18, 4);
                entity.HasOne(p => p.Employee).WithMany().HasForeignKey(p => p.EmployeeId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(p => new { p.CompanyId, p.EmployeeId, p.Year, p.Month }).IsUnique();
            });

            modelBuilder.Entity<AccountingFirm>(entity =>
            {
                entity.ToTable("AccountingFirms");
                entity.HasKey(f => f.Id);
                entity.Property(f => f.Name).IsRequired().HasMaxLength(256);
                entity.Property(f => f.Ice).HasMaxLength(32);
                entity.Property(f => f.TaxId).HasMaxLength(32);
                entity.Property(f => f.FirmCompanyId).IsRequired().HasMaxLength(36);
                entity.HasIndex(f => f.FirmCompanyId).IsUnique();
            });

            modelBuilder.Entity<AccountingFirmClient>(entity =>
            {
                entity.ToTable("AccountingFirmClients");
                entity.HasKey(c => c.Id);
                entity.Property(c => c.ClientCompanyId).IsRequired().HasMaxLength(36);
                entity.Property(c => c.MissionLevel).IsRequired().HasMaxLength(32);
                entity.HasOne(c => c.Firm).WithMany(f => f.Clients).HasForeignKey(c => c.AccountingFirmId).OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(c => new { c.AccountingFirmId, c.ClientCompanyId }).IsUnique();
            });

            modelBuilder.Entity<AccountingAnnotation>(entity =>
            {
                entity.ToTable("AccountingAnnotations");
                entity.HasKey(a => a.Id);
                entity.Property(a => a.CompanyId).HasMaxLength(36);
                entity.Property(a => a.Type).IsRequired().HasMaxLength(32);
                entity.Property(a => a.Message).IsRequired().HasMaxLength(2000);
                entity.Property(a => a.Author).HasMaxLength(128);
                entity.HasIndex(a => new { a.CompanyId, a.IsResolved });
            });

            modelBuilder.Entity<DocumentAuditLog>(entity =>
            {
                entity.ToTable("DocumentAuditLogs");
                entity.HasKey(a => a.Id);
                entity.Property(a => a.DocumentType).IsRequired().HasMaxLength(40);
                entity.Property(a => a.Action).IsRequired().HasMaxLength(40);
                entity.Property(a => a.Summary).HasMaxLength(500);
                entity.Property(a => a.Actor).HasMaxLength(128);
                entity.Property(a => a.CompanyId).HasMaxLength(36);
                entity.HasIndex(a => new { a.DocumentType, a.DocumentId, a.CreatedAt });
                entity.HasIndex(a => a.CompanyId);
            });

            modelBuilder.Entity<EntityAuditLog>(entity =>
            {
                entity.ToTable("EntityAuditLogs");
                entity.HasKey(a => a.Id);
                entity.Property(a => a.EntityType).IsRequired().HasMaxLength(80);
                entity.Property(a => a.EntityKey).IsRequired().HasMaxLength(64);
                entity.Property(a => a.Action).IsRequired().HasMaxLength(40);
                entity.Property(a => a.Summary).HasMaxLength(500);
                entity.Property(a => a.Actor).HasMaxLength(128);
                entity.Property(a => a.CompanyId).HasMaxLength(36);
                entity.HasIndex(a => new { a.CompanyId, a.CreatedAt });
                entity.HasIndex(a => new { a.EntityType, a.EntityKey, a.CreatedAt });
            });

            modelBuilder.Entity<Document>(entity =>
            {
                entity.Property(d => d.CompanyId).HasMaxLength(36);
                entity.HasIndex(d => new { d.TypeDocument, d.Numero, d.CompanyId });
            });

            modelBuilder.Entity<SalesReturn>(entity =>
            {
                entity.ToTable("SalesReturns");
                entity.HasKey(r => r.Id);
                entity.Property(r => r.CompanyId).HasMaxLength(36);
                entity.HasIndex(r => new { r.ReturnNumber, r.CompanyId }).IsUnique();
                entity.Property(r => r.ReturnNumber).IsRequired().HasMaxLength(64);
                entity.Property(r => r.TotalHT).HasPrecision(18, 4);
                entity.Property(r => r.TotalVat).HasPrecision(18, 4);
                entity.Property(r => r.TotalTTC).HasPrecision(18, 4);
                entity.Property(r => r.CurrencyCode).HasMaxLength(8);
                entity.HasOne(r => r.Customer).WithMany().HasForeignKey(r => r.CustomerId);
                entity.HasOne(r => r.SalesDeliveryNote).WithMany().HasForeignKey(r => r.SalesDeliveryNoteId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(r => r.SalesOrder).WithMany().HasForeignKey(r => r.SalesOrderId).IsRequired(false);
                entity.HasIndex(r => r.IsDeleted);
            });

            modelBuilder.Entity<SalesReturnLine>(entity =>
            {
                entity.ToTable("SalesReturnLines");
                entity.HasKey(l => l.Id);
                entity.Property(l => l.UnitPrice).HasPrecision(18, 4);
                entity.Property(l => l.Quantity).HasPrecision(18, 4);
                entity.Property(l => l.VatRate).HasPrecision(18, 4);
                entity.Property(l => l.TotalHT).HasPrecision(18, 4);
                entity.Property(l => l.TotalTTC).HasPrecision(18, 4);
                entity.HasOne(l => l.SalesReturn).WithMany(r => r.Lines).HasForeignKey(l => l.SalesReturnId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SupplierCreditNoteEntity>(entity =>
            {
                entity.ToTable("SupplierCreditNotes");
                entity.HasKey(c => c.Id);
                entity.Property(c => c.CompanyId).HasMaxLength(36);
                entity.HasIndex(c => new { c.CreditNoteNumber, c.CompanyId }).IsUnique();
                entity.Property(c => c.CreditNoteNumber).IsRequired().HasMaxLength(64);
                entity.Property(c => c.TotalHT).HasPrecision(18, 4);
                entity.Property(c => c.TotalVat).HasPrecision(18, 4);
                entity.Property(c => c.TotalTTC).HasPrecision(18, 4);
                entity.HasOne(c => c.Supplier).WithMany().HasForeignKey(c => c.SupplierId);
                entity.HasOne(c => c.SupplierInvoice).WithMany().HasForeignKey(c => c.SupplierInvoiceId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<SupplierCreditNoteLineEntity>(entity =>
            {
                entity.ToTable("SupplierCreditNoteLines");
                entity.HasKey(l => l.Id);
                entity.Property(l => l.UnitPrice).HasPrecision(18, 4);
                entity.Property(l => l.Quantity).HasPrecision(18, 4);
                entity.Property(l => l.VatRate).HasPrecision(18, 4);
                entity.Property(l => l.TotalHT).HasPrecision(18, 4);
                entity.Property(l => l.TotalTTC).HasPrecision(18, 4);
                entity.HasOne(l => l.SupplierCreditNote).WithMany(c => c.Lines).HasForeignKey(l => l.SupplierCreditNoteEntityId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Proforma>(entity =>
            {
                entity.ToTable("Proformas");
                entity.HasKey(p => p.Id);
                entity.Property(p => p.CompanyId).HasMaxLength(36);
                entity.HasIndex(p => new { p.ProformaNumber, p.CompanyId }).IsUnique();
                entity.Property(p => p.ProformaNumber).IsRequired().HasMaxLength(64);
                entity.Property(p => p.TotalHT).HasPrecision(18, 4);
                entity.Property(p => p.TotalVat).HasPrecision(18, 4);
                entity.Property(p => p.TotalTTC).HasPrecision(18, 4);
                entity.Property(p => p.CurrencyCode).HasMaxLength(8);
                entity.HasOne(p => p.Customer).WithMany().HasForeignKey(p => p.CustomerId);
                entity.HasOne(p => p.Quote).WithMany().HasForeignKey(p => p.QuoteId).OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(p => p.SalesOrder).WithMany().HasForeignKey(p => p.SalesOrderId).OnDelete(DeleteBehavior.SetNull);
                entity.HasIndex(p => p.IsDeleted);
            });

            modelBuilder.Entity<ProformaLine>(entity =>
            {
                entity.ToTable("ProformaLines");
                entity.HasKey(l => l.Id);
                entity.Property(l => l.UnitPrice).HasPrecision(18, 4);
                entity.Property(l => l.Quantity).HasPrecision(18, 4);
                entity.Property(l => l.VatRate).HasPrecision(18, 4);
                entity.Property(l => l.TotalHT).HasPrecision(18, 4);
                entity.Property(l => l.TotalTTC).HasPrecision(18, 4);
                entity.HasOne(l => l.Proforma).WithMany(p => p.Lines).HasForeignKey(l => l.ProformaId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<DepositInvoice>(entity =>
            {
                entity.ToTable("DepositInvoices");
                entity.HasKey(d => d.Id);
                entity.Property(d => d.CompanyId).HasMaxLength(36);
                entity.HasIndex(d => new { d.DepositNumber, d.CompanyId }).IsUnique();
                entity.Property(d => d.DepositNumber).IsRequired().HasMaxLength(64);
                entity.Property(d => d.AmountHT).HasPrecision(18, 4);
                entity.Property(d => d.VatRate).HasPrecision(18, 4);
                entity.Property(d => d.AmountTTC).HasPrecision(18, 4);
                entity.Property(d => d.CurrencyCode).HasMaxLength(8);
                entity.HasOne(d => d.Customer).WithMany().HasForeignKey(d => d.CustomerId);
                entity.HasOne(d => d.SalesOrder).WithMany().HasForeignKey(d => d.SalesOrderId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(d => d.AppliedSalesInvoice).WithMany().HasForeignKey(d => d.AppliedSalesInvoiceId).OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<SupplierRfq>(entity =>
            {
                entity.ToTable("SupplierRfqs");
                entity.HasKey(r => r.Id);
                entity.Property(r => r.CompanyId).HasMaxLength(36);
                entity.HasIndex(r => new { r.RfqNumber, r.CompanyId }).IsUnique();
                entity.Property(r => r.RfqNumber).IsRequired().HasMaxLength(64);
                entity.HasOne(r => r.Supplier).WithMany().HasForeignKey(r => r.SupplierId).OnDelete(DeleteBehavior.SetNull);
                entity.HasIndex(r => r.IsDeleted);
            });

            modelBuilder.Entity<SupplierRfqLine>(entity =>
            {
                entity.ToTable("SupplierRfqLines");
                entity.HasKey(l => l.Id);
                entity.Property(l => l.Quantity).HasPrecision(18, 4);
                entity.Property(l => l.EstimatedUnitPrice).HasPrecision(18, 4);
                entity.HasOne(l => l.SupplierRfq).WithMany(r => r.Lines).HasForeignKey(l => l.SupplierRfqId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SupplierReturn>(entity =>
            {
                entity.ToTable("SupplierReturns");
                entity.HasKey(r => r.Id);
                entity.Property(r => r.CompanyId).HasMaxLength(36);
                entity.HasIndex(r => new { r.ReturnNumber, r.CompanyId }).IsUnique();
                entity.Property(r => r.ReturnNumber).IsRequired().HasMaxLength(64);
                entity.Property(r => r.TotalHT).HasPrecision(18, 4);
                entity.Property(r => r.TotalVat).HasPrecision(18, 4);
                entity.Property(r => r.TotalTTC).HasPrecision(18, 4);
                entity.Property(r => r.CurrencyCode).HasMaxLength(8);
                entity.HasOne(r => r.Supplier).WithMany().HasForeignKey(r => r.SupplierId);
                entity.HasOne(r => r.PurchaseOrder).WithMany().HasForeignKey(r => r.PurchaseOrderId).OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(r => r.Receipt).WithMany().HasForeignKey(r => r.ReceiptId).OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(r => r.SupplierInvoice).WithMany().HasForeignKey(r => r.SupplierInvoiceId).OnDelete(DeleteBehavior.SetNull);
                entity.HasIndex(r => r.IsDeleted);
            });

            modelBuilder.Entity<SupplierReturnLine>(entity =>
            {
                entity.ToTable("SupplierReturnLines");
                entity.HasKey(l => l.Id);
                entity.Property(l => l.UnitPrice).HasPrecision(18, 4);
                entity.Property(l => l.Quantity).HasPrecision(18, 4);
                entity.Property(l => l.VatRate).HasPrecision(18, 4);
                entity.Property(l => l.TotalHT).HasPrecision(18, 4);
                entity.Property(l => l.TotalTTC).HasPrecision(18, 4);
                entity.HasOne(l => l.SupplierReturn).WithMany(r => r.Lines).HasForeignKey(l => l.SupplierReturnId).OnDelete(DeleteBehavior.Cascade);
            });

            // RG-RG2 lite : trace d'audit d'allocation de règlement par lot.
            modelBuilder.Entity<PaymentAllocation>(entity =>
            {
                entity.ToTable("PaymentAllocations");
                entity.HasKey(a => a.Id);
                entity.Property(a => a.CompanyId).HasMaxLength(36);
                entity.Property(a => a.Amount).HasPrecision(18, 4);
                entity.HasOne(a => a.Payment).WithMany().HasForeignKey(a => a.PaymentId).OnDelete(DeleteBehavior.SetNull);
                entity.HasIndex(a => a.BatchId);
                entity.HasIndex(a => a.SalesInvoiceId);
            });

            // RG-LT1–4 lite : lettrage client simplifié (saisie manuelle).
            modelBuilder.Entity<LetteringGroup>(entity =>
            {
                entity.ToTable("LetteringGroups");
                entity.HasKey(g => g.Id);
                entity.Property(g => g.CompanyId).HasMaxLength(36);
                entity.Property(g => g.LetteringCode).IsRequired().HasMaxLength(64);
                entity.HasIndex(g => new { g.LetteringCode, g.CompanyId }).IsUnique();
                entity.HasOne(g => g.Customer).WithMany().HasForeignKey(g => g.CustomerId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<LetteringLine>(entity =>
            {
                entity.ToTable("LetteringLines");
                entity.HasKey(l => l.Id);
                entity.Property(l => l.Amount).HasPrecision(18, 4);
                entity.HasOne(l => l.LetteringGroup).WithMany(g => g.Lines).HasForeignKey(l => l.LetteringGroupId).OnDelete(DeleteBehavior.Cascade);
            });

            // RG-PT1–5 lite : tarif spécifique client.
            modelBuilder.Entity<CustomerPriceListItem>(entity =>
            {
                entity.ToTable("CustomerPriceListItems");
                entity.HasKey(p => p.Id);
                entity.Property(p => p.CompanyId).HasMaxLength(36);
                entity.Property(p => p.ProductKey).IsRequired().HasMaxLength(128);
                entity.Property(p => p.UnitPrice).HasPrecision(18, 4);
                entity.Property(p => p.VatRate).HasPrecision(18, 4);
                entity.HasOne(p => p.Customer).WithMany().HasForeignKey(p => p.CustomerId).OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(p => new { p.CustomerId, p.ProductKey });
            });

            modelBuilder.Entity<CompanyEmailSettings>(entity =>
            {
                entity.ToTable("CompanyEmailSettings");
                entity.HasKey(s => s.CompanyId);
                entity.Property(s => s.CompanyId).HasMaxLength(36);
                entity.Property(s => s.SmtpHost).HasMaxLength(255);
                entity.Property(s => s.Username).HasMaxLength(255);
                entity.Property(s => s.Password).HasMaxLength(512);
                entity.Property(s => s.FromEmail).HasMaxLength(255);
                entity.Property(s => s.FromDisplayName).HasMaxLength(255);
                entity.Property(s => s.DefaultReplyTo).HasMaxLength(255);
                entity.Property(s => s.UpdatedBy).HasMaxLength(128);
                entity.Property(s => s.StockAlertRecipients).HasMaxLength(1000);
                entity.HasOne<Company>().WithMany().HasForeignKey(s => s.CompanyId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<EmailMessage>(entity =>
            {
                entity.ToTable("EmailMessages");
                entity.HasKey(m => m.Id);
                entity.Property(m => m.CompanyId).HasMaxLength(36);
                entity.Property(m => m.TrackingId).HasMaxLength(64);
                entity.Property(m => m.TemplateCode).HasMaxLength(64);
                entity.Property(m => m.DocumentType).HasMaxLength(64);
                entity.Property(m => m.DocumentNumber).HasMaxLength(128);
                entity.Property(m => m.ToEmail).HasMaxLength(255);
                entity.Property(m => m.CcEmails).HasMaxLength(1024);
                entity.Property(m => m.ReplyTo).HasMaxLength(255);
                entity.Property(m => m.Subject).HasMaxLength(500);
                entity.Property(m => m.AttachmentFileName).HasMaxLength(255);
                entity.Property(m => m.Status).HasMaxLength(32);
                entity.Property(m => m.LastError).HasMaxLength(500);
                entity.Property(m => m.CreatedBy).HasMaxLength(128);
                entity.HasIndex(m => new { m.CompanyId, m.CreatedAt });
                entity.HasIndex(m => new { m.Status, m.ScheduledAt });
                entity.HasIndex(m => new { m.DocumentType, m.DocumentId });
            });

            // Traçabilité CreatedBy / UpdatedBy (varchar 128) pour toutes les entités IHasAuditTrail.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (!typeof(IHasAuditTrail).IsAssignableFrom(entityType.ClrType))
                    continue;
                modelBuilder.Entity(entityType.ClrType, b =>
                {
                    b.Property(nameof(IHasAuditTrail.CreatedBy)).HasMaxLength(128);
                    b.Property(nameof(IHasAuditTrail.UpdatedBy)).HasMaxLength(128);
                });
            }
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string connectionString = this.configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

            var configuredVersion = this.configuration["Database:ServerVersion"];
            var serverVersion = !string.IsNullOrWhiteSpace(configuredVersion)
                ? ServerVersion.Parse(configuredVersion)
                : ServerVersion.AutoDetect(connectionString);

            optionsBuilder.UseMySql(connectionString, serverVersion);
            optionsBuilder.LogTo(Console.WriteLine);
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
        }
    }
}
