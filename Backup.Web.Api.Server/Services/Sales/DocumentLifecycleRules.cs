using System;
using System.Linq;
using Backup.Web.Api.Server.Brokers.Storage;

namespace Backup.Web.Api.Server.Services.Sales
{
    /// <summary>
    /// Gates de cycle de vie P2 : Proforma, Acompte, DPF, BRF, AF, refund avoir, lettrage.
    /// </summary>
    public static class DocumentLifecycleRules
    {
        public static string? RejectIfNotDraft(string? status, string documentLabel)
        {
            if (string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase)) return null;
            return $"Un {documentLabel} au statut {status} ne peut plus être modifié (Draft uniquement).";
        }

        // --- RG-PF1–4 Proforma ---

        public static string? RejectIfProformaCannotSend(string? status)
        {
            if (string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase)) return null;
            return $"Proforma déjà au statut {status}.";
        }

        public static string? RejectIfProformaCannotCancel(string? status)
        {
            if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return "Proforma déjà annulée.";
            return null;
        }

        public static string? RejectIfProformaCannotDelete(string? status)
        {
            if (string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase)) return null;
            return "Seules les proformas Draft peuvent être supprimées. Sinon annulez.";
        }

        // --- RG-AA1–4 Acompte ---

        public static string? RejectIfDepositOrderUnusable(string? orderStatus)
        {
            if (string.IsNullOrWhiteSpace(orderStatus))
                return "Statut de commande invalide pour un acompte.";

            // RG-AA1 / RG-ER3 : acompte sur commande engagée (pas brouillon / pending / annulée).
            // Closed autorisé : acompte encore possible avant facture de solde.
            if (string.Equals(orderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return "Impossible de créer un acompte sur une commande annulée.";
            if (string.Equals(orderStatus, "Draft", StringComparison.OrdinalIgnoreCase))
                return "Validez / confirmez la commande avant de créer un acompte.";
            if (string.Equals(orderStatus, "Pending", StringComparison.OrdinalIgnoreCase))
                return "Commande en attente d'approbation : acompte non autorisé.";

            // Closed : OK au niveau statut ; si factures déjà soldées → RejectIfClosedOrderFullySettled.
            var allowed =
                string.Equals(orderStatus, "Confirmed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(orderStatus, "PartiallyDelivered", StringComparison.OrdinalIgnoreCase)
                || string.Equals(orderStatus, "Closed", StringComparison.OrdinalIgnoreCase);

            if (!allowed)
                return $"Statut commande « {orderStatus} » non éligible pour un acompte (Confirmed, PartiallyDelivered ou Closed avec reste dû).";

            return null;
        }

        /// <summary>
        /// Commande Closed + toutes factures liées payées / soldées → nouvel acompte interdit.
        /// Closed = livré+facturé ; Paid / reste dû 0 = règlements+avoirs couvrent le TTC.
        /// </summary>
        public static string? RejectIfClosedOrderFullySettled(IStorageBroker storage, int salesOrderId)
        {
            var invoices = storage.SelectAllSalesInvoices()
                .Where(i => i.SalesOrderId == salesOrderId)
                .AsEnumerable()
                .Where(i => !string.Equals(i.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(i.Status, "Draft", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (invoices.Count == 0)
                return null;

            var anyOpen = invoices.Any(inv =>
            {
                if (string.Equals(inv.Status, "Paid", StringComparison.OrdinalIgnoreCase))
                    return false;
                SalesInvoiceSettlement.Enrich(inv, storage);
                return inv.RemainingAmount > 0.01m;
            });

            if (!anyOpen)
                return "Impossible de créer un acompte : la commande est Closed et ses factures sont déjà soldées (payées).";

            return null;
        }

        /// <summary>Statuts commande éligibles à la création d'un acompte (liste UI — Closed filtré côté factures).</summary>
        public static bool IsDepositEligibleOrderStatus(string? orderStatus) =>
            RejectIfDepositOrderUnusable(orderStatus) == null;

        public static string? RejectIfDepositExceedsOrder(decimal depositAmountTtc, decimal orderTotalTtc, string? orderNumber)
        {
            if (depositAmountTtc > orderTotalTtc + 0.01m)
            {
                return $"L'acompte ({depositAmountTtc:0.##} €) ne peut pas dépasser le TTC de la commande {orderNumber} ({orderTotalTtc:0.##} €).";
            }

            return null;
        }

        public static string? RejectIfDepositAmountInvalid(decimal amountHt)
        {
            if (amountHt <= 0) return "Le montant HT de l'acompte doit être positif.";
            return null;
        }

        public static string? RejectIfDepositCannotValidate(string? status)
        {
            if (string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase)) return null;
            return $"Acompte déjà {status}.";
        }

        public static string? RejectIfDepositCannotApply(string? status)
        {
            if (string.Equals(status, "Applied", StringComparison.OrdinalIgnoreCase))
                return "Acompte déjà appliqué.";
            if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return "Un acompte annulé ne peut pas être appliqué.";
            if (string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase))
                return "L'acompte doit être Validated avant d'être appliqué.";
            return null;
        }

        public static string? RejectIfDepositCannotCancel(string? status)
        {
            if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return "Acompte déjà annulé.";
            if (string.Equals(status, "Applied", StringComparison.OrdinalIgnoreCase))
                return "Un acompte déjà appliqué à une facture ne peut pas être annulé directement (passez par un avoir sur la facture).";
            return null;
        }

        // --- RG-DPF1–4 ---

        public static string? RejectIfRfqCannotSend(string? status)
        {
            if (string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase)) return null;
            return $"DPF déjà au statut {status}.";
        }

        public static string? RejectIfRfqCannotAwait(string? status)
        {
            if (string.Equals(status, "Sent", StringComparison.OrdinalIgnoreCase)) return null;
            return $"Seule une DPF Envoyée peut passer en attente (statut actuel : {status}).";
        }

        public static string? RejectIfRfqCannotCancel(string? status)
        {
            if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return "DPF déjà annulée.";
            if (string.Equals(status, "Processed", StringComparison.OrdinalIgnoreCase))
                return "Une DPF déjà convertie en commande fournisseur ne peut plus être annulée.";
            return null;
        }

        public static string? RejectIfRfqCannotConvert(string? status)
        {
            if (string.Equals(status, "Processed", StringComparison.OrdinalIgnoreCase))
                return "DPF déjà convertie.";
            if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return "Une DPF annulée ne peut pas être convertie.";
            return null;
        }

        // --- RG-BRF1–5 ---

        public static string? RejectIfSupplierReturnCannotShip(string? status)
        {
            if (string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase)) return null;
            return $"Retour fournisseur au statut {status} — expédition non applicable (Draft requis).";
        }

        public static string? RejectIfSupplierReturnCannotCancel(string? status, bool hasCreditNote)
        {
            if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return "Retour fournisseur déjà annulé.";
            if (hasCreditNote)
                return "Un retour fournisseur ayant déjà généré un avoir ne peut plus être annulé.";
            return null;
        }

        public static string? RejectIfSupplierReturnCannotCreateCreditNote(string? status, bool hasCreditNote)
        {
            if (hasCreditNote) return "Un avoir existe déjà pour ce retour fournisseur.";
            if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return "Un retour fournisseur annulé ne peut pas générer d'avoir.";
            return null;
        }

        // --- RG-AF3–5 ---

        public static string? RejectIfSupplierCreditCannotValidate(string? status)
        {
            if (string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase)) return null;
            return $"Avoir fournisseur déjà {status}.";
        }

        public static string? RejectIfSupplierCreditCannotApply(string? status)
        {
            if (string.Equals(status, "Applied", StringComparison.OrdinalIgnoreCase))
                return "Avoir fournisseur déjà appliqué.";
            if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return "Un avoir fournisseur annulé ne peut pas être appliqué.";
            return null;
        }

        public static string? RejectIfSupplierCreditCannotCancel(string? status)
        {
            if (string.Equals(status, "Applied", StringComparison.OrdinalIgnoreCase))
                return "Un avoir fournisseur déjà appliqué ne peut pas être annulé.";
            if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return "Avoir fournisseur déjà annulé.";
            return null;
        }

        /// <summary>RG-AF2 : Σ avoirs ≤ TTC facture fournisseur.</summary>
        public static string? ValidateSupplierCreditCap(
            decimal invoiceTotalTtc,
            string? invoiceNumber,
            decimal existingCreditsTtc,
            decimal additionalTtc)
        {
            var projected = existingCreditsTtc + additionalTtc;
            if (projected > invoiceTotalTtc + 0.01m)
            {
                return $"L'avoir ({additionalTtc:0.##} €) dépasserait le TTC de la facture {invoiceNumber} ({invoiceTotalTtc:0.##} €).";
            }

            return null;
        }

        // --- RG-AC5 ---

        public static string? RejectIfCreditNoteCannotRefund(string? status)
        {
            if (string.Equals(status, "Refunded", StringComparison.OrdinalIgnoreCase))
                return "Credit note is already refunded.";
            if (string.Equals(status, "Applied", StringComparison.OrdinalIgnoreCase))
                return "Un avoir déjà compensé ne peut pas être remboursé. Annulez la compensation d'abord.";
            if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return "Un avoir annulé ne peut pas être remboursé.";
            if (string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(status))
            {
                return "Validez l'avoir avant remboursement.";
            }

            return null;
        }

        // --- RG-LT / CO4 ---

        public static string? RejectIfCannotUnletter(DateTime? openFiscalPeriodEnd, DateTime asOfUtc)
        {
            if (openFiscalPeriodEnd == null) return null;
            if (asOfUtc.Date > openFiscalPeriodEnd.Value.Date)
            {
                return $"La période comptable est clôturée depuis le {openFiscalPeriodEnd.Value:dd/MM/yyyy} : impossible de délettrer.";
            }

            return null;
        }
    }
}
