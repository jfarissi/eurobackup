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
            //AddSemesterCourseReferences(modelBuilder);
            //AddStudentSemesterCourseReferences(modelBuilder);
            //AddStudentGuardianReferences(modelBuilder);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string connectionString = this.configuration.GetConnectionString("DefaultConnection");
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
            optionsBuilder.LogTo(Console.WriteLine);
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
        }
    }
}
