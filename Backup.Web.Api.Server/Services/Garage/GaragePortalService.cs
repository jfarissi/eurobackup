using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Users;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Backup.Web.Api.Server.Services.Garage
{
    public sealed class GaragePortalService : IGaragePortalService
    {
        private readonly IStorageBroker storage;

        public GaragePortalService(IStorageBroker storage)
        {
            this.storage = storage;
        }

        public async Task<GarageMeDto> GetMeAsync(User user, string companyId, CancellationToken ct)
        {
            var customer = await this.RequireCustomerAsync(user, companyId, ct);
            return new GarageMeDto
            {
                CustomerId = customer.Id,
                CustomerCode = customer.CustomerCode,
                Name = customer.Name,
                Status = customer.Status,
                Email = customer.Email,
                Phone = customer.Phone,
                Balance = customer.Balance,
                CreditLimit = customer.CreditLimit
            };
        }

        public async Task<IReadOnlyList<GarageOrderDto>> GetOrdersAsync(User user, string companyId, CancellationToken ct)
        {
            var customer = await this.RequireCustomerAsync(user, companyId, ct);
            return await this.storage.SelectAllSalesOrders()
                .ForCompany(companyId)
                .Where(o => o.CustomerId == customer.Id)
                .OrderByDescending(o => o.Date)
                .Take(100)
                .Select(o => new GarageOrderDto
                {
                    Id = o.Id,
                    OrderNumber = o.OrderNumber,
                    Date = o.Date,
                    Status = o.Status,
                    TotalTTC = o.TotalTTC,
                    LineCount = o.Lines.Count
                })
                .ToListAsync(ct);
        }

        public async Task<GarageOrderDetailDto> GetOrderAsync(User user, string companyId, int orderId, CancellationToken ct)
        {
            var customer = await this.RequireCustomerAsync(user, companyId, ct);
            var order = await this.storage.SelectAllSalesOrders()
                .ForCompany(companyId)
                .Where(o => o.Id == orderId && o.CustomerId == customer.Id)
                .FirstOrDefaultAsync(ct)
                ?? throw new KeyNotFoundException("Commande introuvable.");

            return new GarageOrderDetailDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                Date = order.Date,
                Status = order.Status,
                TotalHT = order.TotalHT,
                TotalTTC = order.TotalTTC,
                LineCount = order.Lines?.Count ?? 0,
                Lines = (order.Lines ?? new List<SalesOrderLine>())
                    .OrderBy(l => l.LineNumber)
                    .Select(l => new GarageOrderLineDto
                    {
                        LineNumber = l.LineNumber,
                        ProductKey = l.ProductKey,
                        Description = l.Description,
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice,
                        TotalTTC = l.TotalTTC
                    })
                    .ToList()
            };
        }

        public async Task<IReadOnlyList<GarageVehicleDto>> GetVehiclesAsync(User user, string companyId, CancellationToken ct)
        {
            var customer = await this.RequireCustomerAsync(user, companyId, ct);
            return await this.storage.SelectAllErpPlateVehicles()
                .ForCompany(companyId)
                .Where(v => v.CustomerId == customer.Id)
                .OrderBy(v => v.PlateNumber)
                .Select(v => new GarageVehicleDto
                {
                    Id = v.Id,
                    PlateNumber = v.PlateNumber,
                    Country = v.Country,
                    Vin = v.Vin,
                    KType = v.KType,
                    Make = v.Make,
                    Model = v.Model,
                    Year = v.Year
                })
                .ToListAsync(ct);
        }

        private async Task<Customer> RequireCustomerAsync(User user, string companyId, CancellationToken ct)
        {
            if (!user.CustomerId.HasValue || user.CustomerId.Value <= 0)
                throw new InvalidOperationException("Compte garage non lié à un client.");

            var customer = await this.storage.SelectAllCustomers()
                .ForCompany(companyId)
                .FirstOrDefaultAsync(c => c.Id == user.CustomerId.Value, ct);

            if (customer == null)
                throw new InvalidOperationException("Client garage introuvable pour cette société.");

            return customer;
        }
    }
}
