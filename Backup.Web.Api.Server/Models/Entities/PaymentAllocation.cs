using System;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities
{
    /// <summary>
    /// RG-RG2 lite : trace d'audit d'une allocation de règlement à une facture,
    /// utilisée par le paiement par lot (POST /api/payments/batch) pour regrouper N paiements sous un même BatchId.
    /// </summary>
    public class PaymentAllocation : IHasCompanyId
    {
        public int Id { get; set; }
        public Guid BatchId { get; set; }
        public int? PaymentId { get; set; }
        public Payment? Payment { get; set; }
        public string? CompanyId { get; set; }
        public int CustomerId { get; set; }
        public int SalesInvoiceId { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
