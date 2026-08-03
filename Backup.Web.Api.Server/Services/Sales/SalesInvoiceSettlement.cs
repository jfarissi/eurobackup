using System;
using System.Linq;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;

namespace Backup.Web.Api.Server.Services.Sales
{
    /// <summary>
    /// PaidAmount = règlements (espèces/carte/virement).
    /// CreditedAmount = avoirs Validated/Applied/Refunded liés (réduisent le reste dû).
    /// Remaining = TotalTTC - PaidAmount - CreditedAmount.
    /// </summary>
    public static class SalesInvoiceSettlement
    {
        /// <summary>
        /// Total avoirs qui réduisent le reste dû.
        /// Inclut Validated (GL déjà passée) et Applied/Refunded — pas Draft/Cancelled.
        /// </summary>
        public static decimal GetAppliedCreditTotal(
            IStorageBroker storage,
            int salesInvoiceId,
            int? excludeCreditNoteId = null)
        {
            return storage.SelectAllCreditNotes()
                .Where(c => c.SalesInvoiceId == salesInvoiceId
                    && (!excludeCreditNoteId.HasValue || c.Id != excludeCreditNoteId.Value))
                .AsEnumerable()
                .Where(IsSettlingCreditStatus)
                .Sum(c => c.TotalTTC);
        }

        public static bool IsSettlingCreditStatus(string? status) =>
            string.Equals(status, "Applied", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Refunded", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Validated", StringComparison.OrdinalIgnoreCase);

        private static bool IsSettlingCreditStatus(CreditNoteEntity c) =>
            IsSettlingCreditStatus(c.Status);

        /// <summary>Seuil « centime » : en dessous, la facture est considérée soldée.</summary>
        public const decimal SettlementTolerance = 0.01m;

        public static void Enrich(SalesInvoice invoice, IStorageBroker storage, bool? hasDeliveredSource = null)
        {
            invoice.CreditedAmount = GetAppliedCreditTotal(storage, invoice.Id);
            var rawRemaining = invoice.TotalTTC - invoice.PaidAmount - invoice.CreditedAmount;
            // Arrondi à 2 décimales + tolérance : évite Reste dû €0.00 avec bouton Payer encore visible.
            invoice.RemainingAmount = rawRemaining <= SettlementTolerance
                ? 0m
                : Math.Round(rawRemaining, 2, MidpointRounding.AwayFromZero);
            invoice.HasDeliveredSource = hasDeliveredSource ?? HasLinkedDeliveredNote(storage, invoice.Id);
            // Statut dérivé pour l'affichage (Paid / PartiallyPaid) sans side-effect DB ici.
            RefreshPaymentStatus(invoice, invoice.CreditedAmount);
        }

        public static bool HasLinkedDeliveredNote(IStorageBroker storage, int salesInvoiceId)
        {
            return storage.SelectAllSalesDeliveryNotes()
                .Any(n => n.SalesInvoiceId == salesInvoiceId
                    && (n.Status == "Delivered" || n.Status == "Invoiced"));
        }

        public static string? ValidatePayable(SalesInvoice invoice, IStorageBroker storage)
        {
            if (string.Equals(invoice.Status, "Draft", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(invoice.Status))
            {
                return "Validez la facture avant d'enregistrer un paiement.";
            }

            if (string.Equals(invoice.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return "Une facture annulée ne peut pas être payée.";

            if (!HasLinkedDeliveredNote(storage, invoice.Id))
            {
                return "Paiement autorisé uniquement pour une facture liée à un BL livré (parcours Commande → BL → Facture).";
            }

            return null;
        }

        public static void RefreshPaymentStatus(SalesInvoice invoice, decimal creditedAmount)
        {
            var remaining = invoice.TotalTTC - invoice.PaidAmount - creditedAmount;
            if (remaining <= SettlementTolerance)
            {
                invoice.Status = "Paid";
                invoice.RemainingAmount = 0m;
            }
            else if (invoice.PaidAmount > 0 || creditedAmount > 0)
            {
                invoice.Status = "PartiallyPaid";
            }
            else if (string.Equals(invoice.Status, "Paid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(invoice.Status, "PartiallyPaid", StringComparison.OrdinalIgnoreCase))
            {
                // Plus aucun règlement ni avoir : revenir au statut validé
                invoice.Status = "Validated";
            }
        }
    }
}
