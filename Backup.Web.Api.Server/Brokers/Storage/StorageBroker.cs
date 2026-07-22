using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Backup.Web.Api.Server.Models.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Backup.Web.Api.Server.Models.Rols;
using Backup.Web.Api.Server.Models;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial class StorageBroker : IdentityDbContext<User, Role, Guid>, IStorageBroker
    {
        private readonly IConfiguration configuration;

        public StorageBroker(IConfiguration configuration)
        {
            this.configuration = configuration;
            //this.Database.EnsureCreated();
            //this.Database.Migrate();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Document>(entity =>
            {
                entity.Property(d => d.TypeDocument).HasMaxLength(64);
                entity.Property(d => d.Numero).HasMaxLength(128);
                entity.Property(d => d.Client).HasMaxLength(256);
                entity.Property(d => d.Supplier).HasMaxLength(256);
                entity.Property(d => d.OriginalFileName).HasMaxLength(512);
                entity.Property(d => d.FilePath).HasMaxLength(1024);
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
                entity.HasIndex(s => s.ProductKey).IsUnique();
                entity.Property(s => s.ProductKey).HasMaxLength(256);
                entity.Property(s => s.Supplier).HasMaxLength(256);
                entity.Property(s => s.Description).HasMaxLength(1024);
                entity.Property(s => s.Unit).HasMaxLength(16);
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
                entity.Property(o => o.SessionId).IsRequired().HasMaxLength(64);
                entity.Property(o => o.Status).IsRequired().HasMaxLength(32);
                entity.Property(o => o.StripeSessionId).HasMaxLength(128);
                entity.Property(o => o.InvoiceNumber).HasMaxLength(64);
                entity.Property(o => o.TotalAmount).HasPrecision(18, 4);
                entity.Property(o => o.InvoiceFileName).HasMaxLength(256);
                entity.Property(o => o.InvoicePdfBase64).HasColumnType("longtext");
                entity.Property(o => o.LinesJson).HasColumnType("longtext");
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
