using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Catalog;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Entities.SaaS;
using Backup.Web.Api.Server.Models.Users;
using Backup.Web.Api.Server.Services.Modules;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backup.Web.Api.Server.Services.Tenancy
{
    /// <summary>Compte Demo portail Garage (F5) : client + user rôle Garage + commandes + véhicules.</summary>
    public sealed class GarageSeedService
    {
        public const string DemoEmail = "garage@demo.local";
        public const string DemoPassword = "GarageDemo!2026";
        public const string DemoCustomerCode = "GARAGE-DEMO";

        private readonly IStorageBroker storage;
        private readonly UserManager<User> userManager;
        private readonly IModuleService modules;
        private readonly ILogger<GarageSeedService> logger;

        public GarageSeedService(
            IStorageBroker storage,
            UserManager<User> userManager,
            IModuleService modules,
            ILogger<GarageSeedService> logger)
        {
            this.storage = storage;
            this.userManager = userManager;
            this.modules = modules;
            this.logger = logger;
        }

        public async Task EnsureDemoGarageAsync()
        {
            var companyId = TenancySeedService.DefaultCompanyId;
            await this.modules.EnsureModuleAsync(companyId, ModuleCodes.AutoParts);

            var customer = await this.EnsureCustomerAsync(companyId);
            var user = await this.EnsureUserAsync(companyId, customer.Id);
            await this.EnsureUserCompanyAsync(user.Id, companyId);
            await this.EnsureVehiclesAsync(companyId, customer.Id);
            await this.EnsureOrdersAsync(companyId, customer.Id);

            this.logger.LogInformation(
                "Garage seed ready: {Email} / {Password} → customer {Code} ({CustomerId})",
                DemoEmail, DemoPassword, customer.CustomerCode, customer.Id);
        }

        private async Task<Customer> EnsureCustomerAsync(string companyId)
        {
            var existing = await this.storage.SelectAllCustomers()
                .FirstOrDefaultAsync(c => c.CompanyId == companyId && c.CustomerCode == DemoCustomerCode);
            if (existing != null) return existing;

            return await this.storage.InsertCustomerAsync(new Customer
            {
                CustomerCode = DemoCustomerCode,
                Name = "Garage Auto Dupont",
                Email = DemoEmail,
                Phone = "+32 2 555 0100",
                City = "Bruxelles",
                Country = "BE",
                Status = "Active",
                CreditLimit = 5000m,
                PaymentTerms = "30 jours",
                CompanyId = companyId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "garage-seed"
            });
        }

        private async Task<User> EnsureUserAsync(string companyId, int customerId)
        {
            var user = await this.userManager.FindByEmailAsync(DemoEmail)
                ?? await this.userManager.FindByNameAsync(DemoEmail);

            if (user == null)
            {
                user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = DemoEmail,
                    UserName = DemoEmail,
                    EmailConfirmed = true,
                    Name = "Dupont",
                    FamilyName = "Garage",
                    Status = UserStatus.Activated,
                    CompanyId = companyId,
                    CustomerId = customerId,
                    CreatedDate = DateTimeOffset.UtcNow,
                    UpdatedDate = DateTimeOffset.UtcNow
                };
                var created = await this.userManager.CreateAsync(user, DemoPassword);
                if (!created.Succeeded)
                    throw new InvalidOperationException(
                        "Garage seed user create failed: " + string.Join(", ", created.Errors.Select(e => e.Description)));
            }
            else
            {
                var changed = false;
                if (user.CustomerId != customerId)
                {
                    user.CustomerId = customerId;
                    changed = true;
                }
                if (!string.Equals(user.CompanyId, companyId, StringComparison.OrdinalIgnoreCase))
                {
                    user.CompanyId = companyId;
                    changed = true;
                }
                if (changed)
                {
                    user.UpdatedDate = DateTimeOffset.UtcNow;
                    await this.userManager.UpdateAsync(user);
                }
            }

            if (!await this.userManager.IsInRoleAsync(user, "Garage"))
            {
                var addRole = await this.userManager.AddToRoleAsync(user, "Garage");
                if (!addRole.Succeeded)
                    this.logger.LogWarning(
                        "Garage seed: role Garage failed: {Errors}",
                        string.Join(", ", addRole.Errors.Select(e => e.Description)));
            }

            return user;
        }

        private async Task EnsureUserCompanyAsync(Guid userId, string companyId)
        {
            if (await this.storage.UserHasCompanyAccessAsync(userId, companyId))
                return;
            await this.storage.InsertUserCompanyAsync(new UserCompany
            {
                UserId = userId,
                CompanyId = companyId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "garage-seed"
            });
        }

        private async Task EnsureVehiclesAsync(string companyId, int customerId)
        {
            var hasAny = await this.storage.SelectAllErpPlateVehicles()
                .AnyAsync(v => v.CompanyId == companyId && v.CustomerId == customerId);
            if (hasAny) return;

            await this.storage.UpsertErpPlateVehicleAsync(new ErpPlateVehicle
            {
                CompanyId = companyId,
                CustomerId = customerId,
                PlateNumber = "1-ABC-234",
                Country = "BE",
                Vin = "VF1BZ0L0632345678",
                Make = "Renault",
                Model = "Clio",
                Year = 2018,
                FuelType = "Diesel",
                KType = "12345",
                Source = "Manual",
                CreatedBy = "garage-seed"
            });
            await this.storage.UpsertErpPlateVehicleAsync(new ErpPlateVehicle
            {
                CompanyId = companyId,
                CustomerId = customerId,
                PlateNumber = "2-DEF-567",
                Country = "BE",
                Vin = "WVWZZZ3CZWE123456",
                Make = "Volkswagen",
                Model = "Golf",
                Year = 2016,
                FuelType = "Essence",
                Source = "Manual",
                CreatedBy = "garage-seed"
            });
        }

        private async Task EnsureOrdersAsync(string companyId, int customerId)
        {
            var hasAny = await this.storage.SelectAllSalesOrders()
                .AnyAsync(o => o.CompanyId == companyId && o.CustomerId == customerId);
            if (hasAny) return;

            await this.storage.InsertSalesOrderAsync(new SalesOrder
            {
                OrderNumber = "CMD-GARAGE-001",
                CustomerId = customerId,
                Date = DateTime.UtcNow.AddDays(-12),
                Status = "Delivered",
                TotalHT = 86.78m,
                TotalVat = 18.22m,
                TotalTTC = 105.00m,
                CurrencyCode = "EUR",
                CompanyId = companyId,
                Notes = "Demo portail garage",
                CreatedBy = "garage-seed",
                Lines =
                {
                    new SalesOrderLine
                    {
                        LineNumber = 1,
                        ProductKey = "FLT-001",
                        Description = "Filtre à huile",
                        Quantity = 2,
                        UnitPrice = 12.50m,
                        VatRate = 21m,
                        TotalHT = 25.00m,
                        TotalTTC = 30.25m
                    },
                    new SalesOrderLine
                    {
                        LineNumber = 2,
                        ProductKey = "PLQ-220",
                        Description = "Plaquettes avant",
                        Quantity = 1,
                        UnitPrice = 61.78m,
                        VatRate = 21m,
                        TotalHT = 61.78m,
                        TotalTTC = 74.75m
                    }
                }
            });

            await this.storage.InsertSalesOrderAsync(new SalesOrder
            {
                OrderNumber = "CMD-GARAGE-002",
                CustomerId = customerId,
                Date = DateTime.UtcNow.AddDays(-2),
                Status = "Confirmed",
                TotalHT = 41.32m,
                TotalVat = 8.68m,
                TotalTTC = 50.00m,
                CurrencyCode = "EUR",
                CompanyId = companyId,
                Notes = "Demo portail garage",
                CreatedBy = "garage-seed",
                Lines =
                {
                    new SalesOrderLine
                    {
                        LineNumber = 1,
                        ProductKey = "AMP-H7",
                        Description = "Ampoule H7",
                        Quantity = 2,
                        UnitPrice = 20.66m,
                        VatRate = 21m,
                        TotalHT = 41.32m,
                        TotalTTC = 50.00m
                    }
                }
            });
        }
    }
}
