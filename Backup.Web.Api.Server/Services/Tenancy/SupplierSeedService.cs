using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backup.Web.Api.Server.Services.Tenancy
{
    /// <summary>Import des fournisseurs Pulse (CompanyId = société active par défaut).</summary>
    public class SupplierSeedService
    {
        /// <summary>Legacy : fournisseurs importés avant alignement sur DefaultCompanyId.</summary>
        public const string PulseCompanyId = "0B470A4F-F073-4B12-B54E-A4C1DC234F67";

        private readonly IStorageBroker storage;
        private readonly ILogger<SupplierSeedService> logger;

        public SupplierSeedService(
            IStorageBroker storage,
            ILogger<SupplierSeedService> logger)
        {
            this.storage = storage;
            this.logger = logger;
        }

        public async Task EnsurePulseSuppliersAsync()
        {
            var companyId = TenancySeedService.DefaultCompanyId;
            await this.MigrateLegacySupplierCompanyIdsAsync(companyId);

            foreach (var seed in PulseSuppliers)
            {
                var existing = await this.storage.SelectAllSuppliers()
                    .FirstOrDefaultAsync(s =>
                        s.SupplierCode == seed.SupplierCode
                        && s.CompanyId == companyId);

                if (existing != null)
                {
                    existing.Name = seed.Name;
                    existing.Email = seed.Email;
                    existing.Phone = seed.Phone;
                    existing.Address = seed.Address;
                    existing.PaymentTerms = seed.PaymentTerms;
                    existing.LeadTimeDays = seed.LeadTimeDays;
                    existing.IsActive = seed.IsActive;
                    existing.UpdatedAt = DateTime.UtcNow;
                    await this.storage.UpdateSupplierAsync(existing);
                    continue;
                }

                seed.CompanyId = companyId;
                await this.storage.InsertSupplierAsync(seed);
                this.logger.LogInformation("Imported Pulse supplier {Code} — {Name}", seed.SupplierCode, seed.Name);
            }
        }

        /// <summary>
        /// Les fournisseurs étaient rattachés à PulseCompanyId alors que les utilisateurs
        /// sont sur DefaultCompanyId → liste vide côté API (filtre ForCompany).
        /// Si le même SupplierCode existe déjà sur la société cible, on ne migre pas
        /// (évite la violation IX_Suppliers_SupplierCode_CompanyId).
        /// </summary>
        private async Task MigrateLegacySupplierCompanyIdsAsync(string targetCompanyId)
        {
            var legacy = await this.storage.SelectAllSuppliers()
                .Where(s => s.CompanyId == PulseCompanyId || s.CompanyId == null || s.CompanyId == "")
                .ToListAsync();

            if (legacy.Count == 0) return;

            var migrated = 0;
            var skipped = 0;

            foreach (var supplier in legacy)
            {
                var conflict = await this.storage.SelectAllSuppliers()
                    .AnyAsync(s =>
                        s.Id != supplier.Id
                        && s.SupplierCode == supplier.SupplierCode
                        && s.CompanyId == targetCompanyId);

                if (conflict)
                {
                    skipped++;
                    this.logger.LogWarning(
                        "Skip migrate supplier {Code} (id={Id}): already exists on company {CompanyId}",
                        supplier.SupplierCode,
                        supplier.Id,
                        targetCompanyId);
                    continue;
                }

                supplier.CompanyId = targetCompanyId;
                supplier.UpdatedAt = DateTime.UtcNow;
                await this.storage.UpdateSupplierAsync(supplier);
                migrated++;
            }

            this.logger.LogInformation(
                "Migrated {Migrated} supplier(s) to company {CompanyId} (skipped {Skipped} duplicates)",
                migrated,
                targetCompanyId,
                skipped);
        }

        private static readonly Supplier[] PulseSuppliers =
        {
            new()
            {
                SupplierCode = "FOU-20260121220657",
                Name = "FF GROUP TOOL INDUSTRIES SA",
                Email = "info@ffgroup-toolindustries.com",
                Phone = "302 118 509 500",
                Address = "9 km Paradromos ATTIKI ODOS (exit 4), 19300 ASPROPYRGOS ATTICA, GREECE",
                PaymentTerms = "60D AFTER EOM TTRANSFER",
                LeadTimeDays = 7,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 21, 21, 7, 0, DateTimeKind.Utc)
            },
            new()
            {
                SupplierCode = "FOU-20251225194754",
                Name = "N et B KNAUF & Cie SComm.",
                Email = "info-be@knauf.com",
                Phone = "04/273/83/11",
                Address = "Rue du Parc Industriel, 1 - B 4480 Engis",
                PaymentTerms = "Tot 07.10.2025 zonder aftrek, Te betalen",
                LeadTimeDays = 7,
                IsActive = true,
                CreatedAt = new DateTime(2025, 12, 25, 18, 47, 56, DateTimeKind.Utc)
            },
            new()
            {
                SupplierCode = "FOU-20260603102131",
                Name = "Pardaen",
                Email = "info@pardaen.be",
                Phone = "+32 (0)2 251 13 85",
                Address = "Haachtsesteenweg 672 bus 1, 1910 Kampenhout",
                PaymentTerms = "30 dagen einde maand",
                LeadTimeDays = 7,
                IsActive = true,
                CreatedAt = new DateTime(2026, 6, 3, 8, 21, 42, DateTimeKind.Utc)
            },
            new()
            {
                SupplierCode = "FOU-20260505145627",
                Name = "PGB Europe",
                Email = "info@pgb-europe.com",
                Phone = "+32 (0)9 272 70 70",
                Address = "Gontrode Heirweg 318, 9090 Merelbeke-Melle - Belgium",
                PaymentTerms = "Betaalbaar in 30 dagen",
                LeadTimeDays = 7,
                IsActive = true,
                CreatedAt = new DateTime(2026, 5, 5, 12, 56, 31, DateTimeKind.Utc)
            },
            new()
            {
                SupplierCode = "FOU-20260604114458",
                Name = "Schrauwen Sanitair & Verwarming NV",
                Email = "info@stg-group.be",
                Phone = "014 24 40 20",
                Address = "Atealaan 34B, B-2200 Herentals",
                PaymentTerms = "60 dagen na factuurdatum",
                LeadTimeDays = 7,
                IsActive = true,
                CreatedAt = new DateTime(2026, 6, 4, 9, 45, 2, DateTimeKind.Utc)
            }
        };
    }
}
