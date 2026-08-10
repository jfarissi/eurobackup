using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.Accounting;
using Backup.Web.Api.Server.Services.Numbering;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Security;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly ICompanyContextService companyContext;
        private readonly INumberingSequenceService numberingService;
        private readonly ILogger<PaymentsController> logger;

        public PaymentsController(
            IStorageBroker storage,
            ICompanyContextService companyContext,
            INumberingSequenceService numberingService,
            ILogger<PaymentsController> logger)
        {
            this.storage = storage;
            this.companyContext = companyContext;
            this.numberingService = numberingService;
            this.logger = logger;
        }

        public class PaymentListItem
        {
            public int Id { get; set; }
            public string? CompanyId { get; set; }
            public int SalesInvoiceId { get; set; }
            public decimal Amount { get; set; }
            public decimal RoundingDifference { get; set; }
            public decimal ReceivedAmount { get; set; }
            public decimal ChangeAmount { get; set; }
            public DateTime PaidAt { get; set; }
            public string? Method { get; set; }
            public string? Reference { get; set; }
            public string? Bank { get; set; }
            public string Status { get; set; } = "Success";
            public int? CashSessionId { get; set; }
            public string? TerminalTransactionId { get; set; }
            public string? CreatedBy { get; set; }
            public DateTime CreatedAt { get; set; }
            public SalesInvoice? SalesInvoice { get; set; }
        }

        /// <summary>Consultation unifiée paiements ventes + achats.</summary>
        public class UnifiedPaymentListItem
        {
            public string Side { get; set; } = "sales"; // sales | purchases
            public int Id { get; set; }
            public DateTime Date { get; set; }
            public decimal Amount { get; set; }
            public string? Method { get; set; }
            public string? Reference { get; set; }
            public string Status { get; set; } = "Success";
            public string? DocumentNumber { get; set; }
            public string? PartyName { get; set; }
            public int InvoiceId { get; set; }
        }

        [HttpGet("all")]
        [RequireAnyPermission(Permissions.InvoiceRead, Permissions.SupplierInvoiceRead)]
        public async Task<IActionResult> GetUnified(
            [FromQuery] string? side = "all",
            [FromQuery] string? status = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] string? search = null)
        {
            try
            {
                var companyId = this.companyContext.GetCurrentCompanyId();
                var sideNorm = (side ?? "all").Trim().ToLowerInvariant();
                if (sideNorm is not ("sales" or "purchases" or "all"))
                    return BadRequest("side doit être sales, purchases ou all.");

                var canSales = User.IsInRole("Admin")
                    || User.HasClaim(PermissionResolver.PermissionClaimType, Permissions.InvoiceRead);
                var canPurchases = User.IsInRole("Admin")
                    || User.HasClaim(PermissionResolver.PermissionClaimType, Permissions.SupplierInvoiceRead);

                if (sideNorm == "sales" && !canSales) return Forbid();
                if (sideNorm == "purchases" && !canPurchases) return Forbid();

                var includeSales = (sideNorm is "sales" or "all") && canSales;
                var includePurchases = (sideNorm is "purchases" or "all") && canPurchases;
                if (!includeSales && !includePurchases) return Forbid();

                var statusNorm = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
                var searchNorm = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLowerInvariant();
                DateTime? fromUtc = from?.Date;
                DateTime? toExclusive = to?.Date.AddDays(1);

                var result = new List<UnifiedPaymentListItem>();

                if (includeSales)
                {
                    var salesQuery = this.storage.SelectAllPayments().ForCompany(companyId);
                    if (statusNorm != null)
                        salesQuery = salesQuery.Where(p => p.Status == statusNorm);
                    if (fromUtc.HasValue)
                        salesQuery = salesQuery.Where(p => p.PaidAt >= fromUtc.Value);
                    if (toExclusive.HasValue)
                        salesQuery = salesQuery.Where(p => p.PaidAt < toExclusive.Value);

                    var salesRows = await salesQuery
                        .OrderByDescending(p => p.PaidAt)
                        .Take(500)
                        .ToListAsync();

                    var invoiceIds = salesRows.Select(p => p.SalesInvoiceId).Distinct().ToList();
                    var invoices = await this.storage.SelectAllSalesInvoices()
                        .AsNoTracking()
                        .Where(i => invoiceIds.Contains(i.Id))
                        .Select(i => new { i.Id, i.InvoiceNumber, i.CustomerId })
                        .ToDictionaryAsync(i => i.Id);

                    var customerIds = invoices.Values.Select(i => i.CustomerId).Distinct().ToList();
                    var customers = await this.storage.SelectAllCustomers()
                        .AsNoTracking()
                        .Where(c => customerIds.Contains(c.Id))
                        .Select(c => new { c.Id, c.Name })
                        .ToDictionaryAsync(c => c.Id);

                    foreach (var p in salesRows)
                    {
                        invoices.TryGetValue(p.SalesInvoiceId, out var inv);
                        string? party = null;
                        if (inv != null && customers.TryGetValue(inv.CustomerId, out var cust))
                            party = cust.Name;

                        if (searchNorm != null)
                        {
                            var hay = $"{inv?.InvoiceNumber} {party} {p.Reference} {p.Method} {p.Status}".ToLowerInvariant();
                            if (!hay.Contains(searchNorm)) continue;
                        }

                        result.Add(new UnifiedPaymentListItem
                        {
                            Side = "sales",
                            Id = p.Id,
                            Date = p.PaidAt,
                            Amount = p.Amount,
                            Method = p.Method,
                            Reference = p.Reference,
                            Status = p.Status,
                            DocumentNumber = inv?.InvoiceNumber,
                            PartyName = party,
                            InvoiceId = p.SalesInvoiceId
                        });
                    }
                }

                if (includePurchases)
                {
                    var purchaseQuery = this.storage.SelectAllSupplierPayments().ForCompany(companyId);
                    if (statusNorm != null)
                        purchaseQuery = purchaseQuery.Where(p => p.Status == statusNorm);
                    if (fromUtc.HasValue)
                        purchaseQuery = purchaseQuery.Where(p => p.PaidAt >= fromUtc.Value);
                    if (toExclusive.HasValue)
                        purchaseQuery = purchaseQuery.Where(p => p.PaidAt < toExclusive.Value);

                    var purchaseRows = await purchaseQuery
                        .OrderByDescending(p => p.PaidAt)
                        .Take(500)
                        .ToListAsync();

                    var invIds = purchaseRows.Select(p => p.SupplierInvoiceId).Distinct().ToList();
                    var supplierInvoices = await this.storage.SelectAllSupplierInvoices()
                        .AsNoTracking()
                        .Where(i => invIds.Contains(i.Id))
                        .Select(i => new { i.Id, i.InvoiceNumber, i.SupplierId })
                        .ToDictionaryAsync(i => i.Id);

                    var supplierIds = supplierInvoices.Values.Select(i => i.SupplierId).Distinct().ToList();
                    var suppliers = await this.storage.SelectAllSuppliers()
                        .AsNoTracking()
                        .Where(s => supplierIds.Contains(s.Id))
                        .Select(s => new { s.Id, s.Name })
                        .ToDictionaryAsync(s => s.Id);

                    foreach (var p in purchaseRows)
                    {
                        supplierInvoices.TryGetValue(p.SupplierInvoiceId, out var inv);
                        string? party = null;
                        if (inv != null && suppliers.TryGetValue(inv.SupplierId, out var sup))
                            party = sup.Name;

                        if (searchNorm != null)
                        {
                            var hay = $"{inv?.InvoiceNumber} {party} {p.Reference} {p.Method} {p.Status}".ToLowerInvariant();
                            if (!hay.Contains(searchNorm)) continue;
                        }

                        result.Add(new UnifiedPaymentListItem
                        {
                            Side = "purchases",
                            Id = p.Id,
                            Date = p.PaidAt,
                            Amount = p.Amount,
                            Method = p.Method,
                            Reference = p.Reference,
                            Status = p.Status,
                            DocumentNumber = inv?.InvoiceNumber,
                            PartyName = party,
                            InvoiceId = p.SupplierInvoiceId
                        });
                    }
                }

                var ordered = result
                    .OrderByDescending(r => r.Date)
                    .Take(500)
                    .ToList();

                return Ok(ordered);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "GET /api/payments/all failed");
                return StatusCode(500, new
                {
                    error = "Impossible de charger les paiements unifiés.",
                    detail = ex.GetBaseException().Message
                });
            }
        }

        [HttpGet]
        [RequirePermission(Permissions.InvoiceRead)]
        public async Task<IActionResult> GetAll([FromQuery] int? salesInvoiceId = null, [FromQuery] string? status = null)
        {
            try
            {
                var companyId = this.companyContext.GetCurrentCompanyId();
                var query = this.storage.SelectAllPayments().ForCompany(companyId);

                if (salesInvoiceId.HasValue && salesInvoiceId.Value > 0)
                {
                    query = query.Where(p => p.SalesInvoiceId == salesInvoiceId.Value);
                }

                if (!string.IsNullOrWhiteSpace(status))
                {
                    var s = status.Trim();
                    query = query.Where(p => p.Status == s);
                }

                var rows = await query
                    .OrderByDescending(p => p.PaidAt)
                    .Take(500)
                    .ToListAsync();

                var invoiceIds = rows.Select(p => p.SalesInvoiceId).Distinct().ToList();
                var invoiceMap = await this.storage.SelectAllSalesInvoices()
                    .AsNoTracking()
                    .Where(i => invoiceIds.Contains(i.Id))
                    .Select(i => new { i.Id, i.InvoiceNumber, i.CustomerId, i.Status, i.TotalTTC, i.PaidAmount })
                    .ToDictionaryAsync(i => i.Id);

                var payments = rows.Select(p =>
                {
                    invoiceMap.TryGetValue(p.SalesInvoiceId, out var inv);
                    return new PaymentListItem
                    {
                        Id = p.Id,
                        CompanyId = p.CompanyId,
                        SalesInvoiceId = p.SalesInvoiceId,
                        Amount = p.Amount,
                        RoundingDifference = p.RoundingDifference,
                        ReceivedAmount = p.ReceivedAmount,
                        ChangeAmount = p.ChangeAmount,
                        PaidAt = p.PaidAt,
                        Method = p.Method,
                        Reference = p.Reference,
                        Bank = p.Bank,
                        Status = p.Status,
                        CashSessionId = p.CashSessionId,
                        TerminalTransactionId = p.TerminalTransactionId,
                        CreatedBy = p.CreatedBy,
                        CreatedAt = p.CreatedAt,
                        SalesInvoice = inv == null
                            ? null
                            : new SalesInvoice
                            {
                                Id = inv.Id,
                                InvoiceNumber = inv.InvoiceNumber,
                                CustomerId = inv.CustomerId,
                                Status = inv.Status,
                                TotalTTC = inv.TotalTTC,
                                PaidAmount = inv.PaidAmount
                            }
                    };
                }).ToList();

                return Ok(payments);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "GET /api/payments failed");
                return StatusCode(500, new
                {
                    error = "Impossible de charger les paiements.",
                    detail = ex.GetBaseException().Message
                });
            }
        }

        [HttpGet("{id:int}")]
        [RequirePermission(Permissions.InvoiceRead)]
        public async Task<IActionResult> GetById(int id)
        {
            var payment = await this.storage.SelectPaymentByIdAsync(id);
            if (payment == null || !payment.BelongsToCompany(this.companyContext.GetCurrentCompanyId()))
            {
                return NotFound();
            }

            return Ok(payment);
        }

        [HttpGet("by-invoice/{salesInvoiceId:int}")]
        [RequirePermission(Permissions.InvoiceRead)]
        public async Task<IActionResult> GetByInvoice(int salesInvoiceId)
        {
            var companyId = this.companyContext.GetCurrentCompanyId();
            var invoice = await this.storage.SelectSalesInvoiceByIdAsync(salesInvoiceId);
            if (invoice == null || !invoice.BelongsToCompany(companyId)) return NotFound();

            var payments = await this.storage.SelectPaymentsBySalesInvoiceId(salesInvoiceId)
                .OrderByDescending(p => p.PaidAt)
                .ToListAsync();

            return Ok(payments);
        }

        public class BatchAllocationRequest
        {
            public int SalesInvoiceId { get; set; }
            public decimal Amount { get; set; }
        }

        public class BatchPaymentRequest
        {
            public int CustomerId { get; set; }
            public string Method { get; set; } = "BankTransfer";
            public string? Reference { get; set; }
            public string? Bank { get; set; }
            public List<BatchAllocationRequest> Allocations { get; set; } = new();
        }

        public class BatchAllocationResult
        {
            public int SalesInvoiceId { get; set; }
            public decimal Amount { get; set; }
            public int? PaymentId { get; set; }
            public string? Error { get; set; }
        }

        /// <summary>
        /// RG-RG2 lite : paiement par lot — répartit un règlement client sur N factures en une seule requête.
        /// Crée un Payment par facture (réutilise le flux existant de règlement + comptabilisation) et une
        /// PaymentAllocation d'audit par ligne, regroupées sous un même BatchId.
        /// </summary>
        [HttpPost("batch")]
        [RequirePermission(Permissions.InvoiceUpdate)]
        public async Task<IActionResult> Batch([FromBody] BatchPaymentRequest request)
        {
            if (request.Allocations == null || request.Allocations.Count == 0)
                return BadRequest("Au moins une allocation (salesInvoiceId, amount) est requise.");
            if (request.Allocations.Any(a => a.Amount <= 0))
                return BadRequest("Chaque montant alloué doit être positif.");

            var companyId = this.companyContext.GetCurrentCompanyId();
            var batchId = Guid.NewGuid();
            var results = new List<BatchAllocationResult>();
            var createdPayments = new List<Payment>();

            foreach (var allocation in request.Allocations)
            {
                var invoice = await this.storage.SelectSalesInvoiceByIdAsync(allocation.SalesInvoiceId);
                if (invoice == null || !invoice.BelongsToCompany(companyId))
                {
                    results.Add(new BatchAllocationResult { SalesInvoiceId = allocation.SalesInvoiceId, Amount = allocation.Amount, Error = "Facture introuvable." });
                    continue;
                }
                if (invoice.CustomerId != request.CustomerId)
                {
                    results.Add(new BatchAllocationResult { SalesInvoiceId = allocation.SalesInvoiceId, Amount = allocation.Amount, Error = "La facture n'appartient pas au client indiqué." });
                    continue;
                }

                var payableError = SalesInvoiceSettlement.ValidatePayable(invoice, this.storage);
                if (payableError != null)
                {
                    results.Add(new BatchAllocationResult { SalesInvoiceId = allocation.SalesInvoiceId, Amount = allocation.Amount, Error = payableError });
                    continue;
                }

                var credited = SalesInvoiceSettlement.GetAppliedCreditTotal(this.storage, invoice.Id);
                var remaining = Math.Max(0m, invoice.TotalTTC - invoice.PaidAmount - credited);
                if (remaining <= 0)
                {
                    results.Add(new BatchAllocationResult { SalesInvoiceId = allocation.SalesInvoiceId, Amount = allocation.Amount, Error = "Facture déjà soldée (règlements + avoirs)." });
                    continue;
                }
                if (allocation.Amount > remaining + 0.01m)
                {
                    results.Add(new BatchAllocationResult
                    {
                        SalesInvoiceId = allocation.SalesInvoiceId,
                        Amount = allocation.Amount,
                        Error = $"Le montant ({allocation.Amount:0.##} €) dépasse le reste dû ({remaining:0.##} €)."
                    });
                    continue;
                }

                invoice.PaidAmount += allocation.Amount;
                SalesInvoiceSettlement.RefreshPaymentStatus(invoice, credited);
                var batchNote = $"Règlement par lot {batchId} : {allocation.Amount:0.##} via {request.Method}";
                invoice.Notes = string.IsNullOrWhiteSpace(invoice.Notes) ? batchNote : $"{invoice.Notes}{Environment.NewLine}{batchNote}";
                await this.storage.UpdateSalesInvoiceAsync(invoice);

                var payment = await this.storage.InsertPaymentAsync(new Payment
                {
                    CompanyId = invoice.CompanyId,
                    SalesInvoiceId = invoice.Id,
                    Amount = allocation.Amount,
                    ReceivedAmount = allocation.Amount,
                    PaidAt = DateTime.UtcNow,
                    Method = request.Method,
                    Reference = request.Reference,
                    Bank = request.Bank,
                    Status = "Success",
                    CreatedBy = SalesDocumentAudit.ActorFrom(User),
                    CreatedAt = DateTime.UtcNow
                });
                createdPayments.Add(payment);

                await this.storage.InsertPaymentAllocationAsync(new PaymentAllocation
                {
                    BatchId = batchId,
                    PaymentId = payment.Id,
                    CompanyId = invoice.CompanyId,
                    CustomerId = request.CustomerId,
                    SalesInvoiceId = invoice.Id,
                    Amount = allocation.Amount,
                    CreatedAt = DateTime.UtcNow
                });

                var (_, payError) = await AccountingLedger.PostSalesPaymentAsync(
                    this.storage, this.numberingService, invoice, payment, SalesDocumentAudit.ActorFrom(User));

                results.Add(new BatchAllocationResult
                {
                    SalesInvoiceId = invoice.Id,
                    Amount = allocation.Amount,
                    PaymentId = payment.Id,
                    Error = payError
                });
            }

            return Ok(new
            {
                batchId,
                totalAllocated = createdPayments.Sum(p => p.Amount),
                payments = createdPayments,
                results
            });
        }

        [HttpPost("{id:int}/cancel")]
        [RequirePermission(Permissions.InvoiceUpdate)]
        public async Task<IActionResult> Cancel(int id)
        {
            var payment = await this.storage.SelectPaymentByIdForUpdateAsync(id);
            if (payment == null || !payment.BelongsToCompany(this.companyContext.GetCurrentCompanyId()))
            {
                return NotFound();
            }

            if (string.Equals(payment.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
                || string.Equals(payment.Status, "Refunded", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Ce paiement est déjà annulé ou remboursé.");
            }

            payment.Status = "Cancelled";
            await this.storage.UpdatePaymentAsync(payment);

            var invoice = await this.storage.SelectSalesInvoiceByIdAsync(payment.SalesInvoiceId);
            if (invoice != null)
            {
                invoice.PaidAmount = Math.Max(0m, invoice.PaidAmount - payment.Amount);
                var credited = SalesInvoiceSettlement.GetAppliedCreditTotal(this.storage, invoice.Id);
                SalesInvoiceSettlement.RefreshPaymentStatus(invoice, credited);
                await this.storage.UpdateSalesInvoiceAsync(invoice);

                // RG-CO4 : écriture inverse du règlement.
                var (_, revError) = await AccountingLedger.ReverseSalesPaymentAsync(
                    this.storage, this.numberingService, payment, invoice, SalesDocumentAudit.ActorFrom(User));
                if (revError != null) return BadRequest(revError);

                var customer = await this.storage.SelectCustomerByIdAsync(invoice.CustomerId);
                if (customer != null)
                {
                    customer.Balance += payment.Amount;
                    customer.UpdatedAt = DateTime.UtcNow;
                    await this.storage.UpdateCustomerAsync(customer);
                }
            }

            return Ok(payment);
        }
    }
}
