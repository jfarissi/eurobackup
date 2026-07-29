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
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Entities.SaaS;

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
                entity.HasIndex(c => c.CustomerCode).IsUnique();
                entity.Property(c => c.CustomerCode).IsRequired().HasMaxLength(64);
                entity.Property(c => c.Name).IsRequired().HasMaxLength(256);
                entity.Property(c => c.Balance).HasPrecision(18, 4);
            });

            modelBuilder.Entity<Supplier>(entity =>
            {
                entity.ToTable("Suppliers");
                entity.HasKey(s => s.Id);
                entity.HasIndex(s => s.SupplierCode).IsUnique();
                entity.Property(s => s.SupplierCode).IsRequired().HasMaxLength(64);
                entity.Property(s => s.Name).IsRequired().HasMaxLength(256);
            });

            modelBuilder.Entity<Quote>(entity =>
            {
                entity.ToTable("Quotes");
                entity.HasKey(q => q.Id);
                entity.HasIndex(q => q.QuoteNumber).IsUnique();
                entity.Property(q => q.QuoteNumber).IsRequired().HasMaxLength(64);
                entity.Property(q => q.TotalHT).HasPrecision(18, 4);
                entity.Property(q => q.TotalVat).HasPrecision(18, 4);
                entity.Property(q => q.TotalTTC).HasPrecision(18, 4);
                entity.HasOne(q => q.Customer).WithMany().HasForeignKey(q => q.CustomerId);
            });

            modelBuilder.Entity<QuoteLine>(entity =>
            {
                entity.ToTable("QuoteLines");
                entity.HasKey(l => l.Id);
                entity.Property(l => l.UnitPrice).HasPrecision(18, 4);
                entity.Property(l => l.Quantity).HasPrecision(18, 4);
                entity.Property(l => l.VatRate).HasPrecision(18, 4);
                entity.Property(l => l.TotalHT).HasPrecision(18, 4);
                entity.Property(l => l.TotalTTC).HasPrecision(18, 4);
                entity.HasOne(l => l.Quote).WithMany(q => q.Lines).HasForeignKey(l => l.QuoteId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SalesOrder>(entity =>
            {
                entity.ToTable("SalesOrders");
                entity.HasKey(o => o.Id);
                entity.HasIndex(o => o.OrderNumber).IsUnique();
                entity.Property(o => o.OrderNumber).IsRequired().HasMaxLength(64);
                entity.Property(o => o.TotalHT).HasPrecision(18, 4);
                entity.Property(o => o.TotalVat).HasPrecision(18, 4);
                entity.Property(o => o.TotalTTC).HasPrecision(18, 4);
                entity.HasOne(o => o.Customer).WithMany().HasForeignKey(o => o.CustomerId);
            });

            modelBuilder.Entity<SalesOrderLine>(entity =>
            {
                entity.ToTable("SalesOrderLines");
                entity.HasKey(l => l.Id);
                entity.Property(l => l.UnitPrice).HasPrecision(18, 4);
                entity.Property(l => l.Quantity).HasPrecision(18, 4);
                entity.Property(l => l.DeliveredQuantity).HasPrecision(18, 4);
                entity.Property(l => l.VatRate).HasPrecision(18, 4);
                entity.Property(l => l.TotalHT).HasPrecision(18, 4);
                entity.Property(l => l.TotalTTC).HasPrecision(18, 4);
                entity.HasOne(l => l.SalesOrder).WithMany(o => o.Lines).HasForeignKey(l => l.SalesOrderId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SalesInvoice>(entity =>
            {
                entity.ToTable("SalesInvoices");
                entity.HasKey(i => i.Id);
                entity.HasIndex(i => i.InvoiceNumber).IsUnique();
                entity.Property(i => i.InvoiceNumber).IsRequired().HasMaxLength(64);
                entity.Property(i => i.TotalHT).HasPrecision(18, 4);
                entity.Property(i => i.TotalVat).HasPrecision(18, 4);
                entity.Property(i => i.TotalTTC).HasPrecision(18, 4);
                entity.Property(i => i.PaidAmount).HasPrecision(18, 4);
                entity.HasOne(i => i.Customer).WithMany().HasForeignKey(i => i.CustomerId);
            });

            modelBuilder.Entity<SalesInvoiceLine>(entity =>
            {
                entity.ToTable("SalesInvoiceLines");
                entity.HasKey(l => l.Id);
                entity.Property(l => l.UnitPrice).HasPrecision(18, 4);
                entity.Property(l => l.Quantity).HasPrecision(18, 4);
                entity.Property(l => l.VatRate).HasPrecision(18, 4);
                entity.Property(l => l.TotalHT).HasPrecision(18, 4);
                entity.Property(l => l.TotalTTC).HasPrecision(18, 4);
                entity.HasOne(l => l.SalesInvoice).WithMany(i => i.Lines).HasForeignKey(l => l.SalesInvoiceId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CreditNoteEntity>(entity =>
            {
                entity.ToTable("CreditNotes");
                entity.HasKey(c => c.Id);
                entity.HasIndex(c => c.CreditNoteNumber).IsUnique();
                entity.Property(c => c.CreditNoteNumber).IsRequired().HasMaxLength(64);
                entity.Property(c => c.TotalHT).HasPrecision(18, 4);
                entity.Property(c => c.TotalVat).HasPrecision(18, 4);
                entity.Property(c => c.TotalTTC).HasPrecision(18, 4);
                entity.HasOne(c => c.Customer).WithMany().HasForeignKey(c => c.CustomerId);
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
                entity.HasIndex(p => p.OrderNumber).IsUnique();
                entity.Property(p => p.OrderNumber).IsRequired().HasMaxLength(64);
                entity.Property(p => p.TotalHT).HasPrecision(18, 4);
                entity.Property(p => p.TotalVat).HasPrecision(18, 4);
                entity.Property(p => p.TotalTTC).HasPrecision(18, 4);
                entity.HasOne(p => p.Supplier).WithMany().HasForeignKey(p => p.SupplierId);
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
                entity.Property(l => l.VatRate).HasPrecision(18, 4);
                entity.Property(l => l.TotalHT).HasPrecision(18, 4);
                entity.Property(l => l.TotalTTC).HasPrecision(18, 4);
                entity.HasOne(l => l.PurchaseOrder).WithMany(p => p.Lines).HasForeignKey(l => l.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SupplierInvoiceEntity>(entity =>
            {
                entity.ToTable("SupplierInvoices");
                entity.HasKey(s => s.Id);
                entity.HasIndex(s => s.InvoiceNumber);
                entity.Property(s => s.InvoiceNumber).IsRequired().HasMaxLength(64);
                entity.Property(s => s.TotalHT).HasPrecision(18, 4);
                entity.Property(s => s.TotalVat).HasPrecision(18, 4);
                entity.Property(s => s.TotalTTC).HasPrecision(18, 4);
                entity.HasOne(s => s.Supplier).WithMany().HasForeignKey(s => s.SupplierId);
                entity.HasIndex(s => s.PurchaseOrderId);
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
                entity.HasIndex(m => m.ProductKey);
                entity.Property(m => m.ProductKey).IsRequired().HasMaxLength(256);
                entity.Property(m => m.Quantity).HasPrecision(18, 4);
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
                entity.HasIndex(c => c.SessionNumber).IsUnique();
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

            modelBuilder.Entity<Receipt>(entity =>
            {
                entity.ToTable("ErpReceipts");
                entity.HasKey(r => r.Id);
                entity.HasIndex(r => r.ReceiptNumber);
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
                entity.HasIndex(n => n.DeliveryNumber).IsUnique();
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

            modelBuilder.Entity<Document>(entity =>
            {
                entity.HasIndex(d => d.CompanyId);
                entity.Property(d => d.CompanyId).HasMaxLength(36);
            });

            modelBuilder.Entity<StockItem>(entity =>
            {
                entity.HasIndex(s => s.CompanyId);
                entity.Property(s => s.CompanyId).HasMaxLength(36);
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
