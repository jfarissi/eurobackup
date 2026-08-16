using System;
using System.Collections.Generic;
using System.Linq;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Services.Stock;

namespace Backup.Web.Api.Server.Services.Sales
{
    /// <summary>Règles P0 du référentiel (A2, T3, T6, V8, T7).</summary>
    public static class SalesBusinessRules
    {
        /// <summary>RG-CT2 : tiers Actif uniquement pour un nouveau document.</summary>
        public static string? RejectIfPartyNotActive(string? partyStatus, string partyLabel)
        {
            var status = string.IsNullOrWhiteSpace(partyStatus) ? "Active" : partyStatus.Trim();
            if (string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Actif", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return $"Le tiers ({partyLabel}) est au statut {status}. Seul un tiers Actif peut être utilisé sur un nouveau document.";
        }

        /// <summary>RG-CT3 : snapshot adresse client.</summary>
        public static string FormatPartyAddress(Customer customer)
        {
            var line2 = string.Join(" ", new[] { customer.PostalCode, customer.City }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p!.Trim()));
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(customer.Name)) parts.Add(customer.Name.Trim());
            if (!string.IsNullOrWhiteSpace(customer.Address)) parts.Add(customer.Address.Trim());
            if (!string.IsNullOrWhiteSpace(line2)) parts.Add(line2);
            if (!string.IsNullOrWhiteSpace(customer.Country)) parts.Add(customer.Country.Trim());
            return string.Join(", ", parts);
        }

        /// <summary>RG-FC8 / RG-CP3 / RG-FA1 / RG-RE1 / RG-RE5 : lignes (remise) → remise en-tête sur marchandises → + frais de port.</summary>
        public static void RecalculateInvoiceTotals(SalesInvoice invoice)
        {
            invoice.Lines ??= new System.Collections.Generic.List<SalesInvoiceLine>();
            invoice.ShippingAmountHt = CapNonNegativeAmount(invoice.ShippingAmountHt);
            invoice.ShippingVatRate = CapNonNegativeAmount(invoice.ShippingVatRate);
            foreach (var line in invoice.Lines)
            {
                line.DiscountPercent = CapDiscountPercent(line.DiscountPercent);
                line.TotalHT = line.Quantity * line.UnitPrice * (1 - (line.DiscountPercent / 100m));
                line.TotalTTC = line.TotalHT * (1 + (line.VatRate / 100m));
            }

            RecalculateDocumentTotals(
                invoice.Lines.Select(l => (l.ProductKey, l.TotalHT, l.TotalTTC)),
                invoice.HeaderDiscountPercent,
                invoice.ShippingAmountHt,
                invoice.ShippingVatRate,
                out var totalHT, out var totalVat, out var totalTTC);
            invoice.TotalHT = totalHT;
            invoice.TotalVat = totalVat;
            invoice.TotalTTC = totalTTC;
        }

        /// <summary>RG-FC8 / RG-CP3 / RG-FA1 : même logique pour devis.</summary>
        public static void RecalculateQuoteTotals(Quote quote)
        {
            quote.Lines ??= new System.Collections.Generic.List<QuoteLine>();
            quote.ShippingAmountHt = CapNonNegativeAmount(quote.ShippingAmountHt);
            quote.ShippingVatRate = CapNonNegativeAmount(quote.ShippingVatRate);
            foreach (var line in quote.Lines)
            {
                line.DiscountPercent = CapDiscountPercent(line.DiscountPercent);
                line.TotalHT = line.Quantity * line.UnitPrice * (1 - (line.DiscountPercent / 100m));
                line.TotalTTC = line.TotalHT * (1 + (line.VatRate / 100m));
            }

            RecalculateDocumentTotals(
                quote.Lines.Select(l => (l.ProductKey, l.TotalHT, l.TotalTTC)),
                quote.HeaderDiscountPercent,
                quote.ShippingAmountHt,
                quote.ShippingVatRate,
                out var totalHT, out var totalVat, out var totalTTC);
            quote.TotalHT = totalHT;
            quote.TotalVat = totalVat;
            quote.TotalTTC = totalTTC;
        }

        /// <summary>RG-FC8 / RG-CP3 / RG-FA1 : même logique pour commandes.</summary>
        public static void RecalculateOrderTotals(SalesOrder order)
        {
            order.Lines ??= new System.Collections.Generic.List<SalesOrderLine>();
            order.ShippingAmountHt = CapNonNegativeAmount(order.ShippingAmountHt);
            order.ShippingVatRate = CapNonNegativeAmount(order.ShippingVatRate);
            foreach (var line in order.Lines)
            {
                line.DiscountPercent = CapDiscountPercent(line.DiscountPercent);
                line.TotalHT = line.Quantity * line.UnitPrice * (1 - (line.DiscountPercent / 100m));
                line.TotalTTC = line.TotalHT * (1 + (line.VatRate / 100m));
            }

            RecalculateDocumentTotals(
                order.Lines.Select(l => (l.ProductKey, l.TotalHT, l.TotalTTC)),
                order.HeaderDiscountPercent,
                order.ShippingAmountHt,
                order.ShippingVatRate,
                out var totalHT, out var totalVat, out var totalTTC);
            order.TotalHT = totalHT;
            order.TotalVat = totalVat;
            order.TotalTTC = totalTTC;
        }

        /// <summary>RG-RM1 / RG-CP3 / RG-FA1 / RG-FA3 : totaux commande fournisseur.</summary>
        public static void RecalculatePurchaseOrderTotals(PurchaseOrder order)
        {
            order.Lines ??= new System.Collections.Generic.List<PurchaseOrderLine>();
            order.ShippingAmountHt = CapNonNegativeAmount(order.ShippingAmountHt);
            order.ShippingVatRate = CapNonNegativeAmount(order.ShippingVatRate);
            foreach (var line in order.Lines)
            {
                line.DiscountPercent = CapDiscountPercent(line.DiscountPercent);
                line.TotalHT = line.Quantity * line.UnitPrice * (1 - (line.DiscountPercent / 100m));
                line.TotalTTC = line.TotalHT * (1 + (line.VatRate / 100m));
            }

            RecalculateDocumentTotals(
                order.Lines.Select(l => (l.ProductKey, l.TotalHT, l.TotalTTC)),
                order.HeaderDiscountPercent,
                order.ShippingAmountHt,
                order.ShippingVatRate,
                out var totalHT, out var totalVat, out var totalTTC);
            order.TotalHT = totalHT;
            order.TotalVat = totalVat;
            order.TotalTTC = totalTTC;
        }

        /// <summary>RG-RM1 / RG-CP3 / RG-FA1 / RG-FA3 : totaux facture fournisseur.</summary>
        public static void RecalculateSupplierInvoiceTotals(SupplierInvoiceEntity invoice)
        {
            invoice.Lines ??= new System.Collections.Generic.List<SupplierInvoiceLineEntity>();
            invoice.ShippingAmountHt = CapNonNegativeAmount(invoice.ShippingAmountHt);
            invoice.ShippingVatRate = CapNonNegativeAmount(invoice.ShippingVatRate);
            foreach (var line in invoice.Lines)
            {
                line.DiscountPercent = CapDiscountPercent(line.DiscountPercent);
                line.TotalHT = line.Quantity * line.UnitPrice * (1 - (line.DiscountPercent / 100m));
                line.TotalTTC = line.TotalHT * (1 + (line.VatRate / 100m));
            }

            RecalculateDocumentTotals(
                invoice.Lines.Select(l => (l.ProductKey, l.TotalHT, l.TotalTTC)),
                invoice.HeaderDiscountPercent,
                invoice.ShippingAmountHt,
                invoice.ShippingVatRate,
                out var totalHT, out var totalVat, out var totalTTC);
            invoice.TotalHT = totalHT;
            invoice.TotalVat = totalVat;
            invoice.TotalTTC = totalTTC;
        }

        /// <summary>
        /// RG-RE1 : remise en-tête sur marchandises uniquement.
        /// RG-RE5 / RG-FA1 : FDP (en-tête ou lignes FDP/SHIPPING) hors remise en-tête, ajoutés ensuite.
        /// </summary>
        public static void RecalculateDocumentTotals(
            IEnumerable<(string? ProductKey, decimal TotalHT, decimal TotalTTC)> lines,
            decimal headerDiscountPercent,
            decimal shippingAmountHt,
            decimal shippingVatRate,
            out decimal totalHT,
            out decimal totalVat,
            out decimal totalTTC)
        {
            decimal merchHT = 0m, merchVat = 0m, shipLineHT = 0m, shipLineVat = 0m;
            foreach (var line in lines)
            {
                var lineVat = line.TotalTTC - line.TotalHT;
                if (StockLedger.IsShippingFeeKey(line.ProductKey))
                {
                    shipLineHT += line.TotalHT;
                    shipLineVat += lineVat;
                }
                else
                {
                    merchHT += line.TotalHT;
                    merchVat += lineVat;
                }
            }

            var merchTTC = merchHT + merchVat;
            ApplyHeaderDiscount(headerDiscountPercent, ref merchHT, ref merchVat, ref merchTTC);

            var shipHeaderHT = CapNonNegativeAmount(shippingAmountHt);
            var shipHeaderVat = shipHeaderHT * (CapNonNegativeAmount(shippingVatRate) / 100m);

            totalHT = merchHT + shipLineHT + shipHeaderHT;
            totalVat = merchVat + shipLineVat + shipHeaderVat;
            totalTTC = totalHT + totalVat;
        }

        /// <summary>RG-RM1–5 : borne la remise ligne/pied de page entre 0 et 100%.</summary>
        public static decimal CapDiscountPercent(decimal discountPercent) =>
            discountPercent < 0 ? 0m : (discountPercent > 100 ? 100m : discountPercent);

        /// <summary>RG-FA1 : borne un montant (HT/TVA port) à ≥ 0.</summary>
        public static decimal CapNonNegativeAmount(decimal amount) => amount < 0 ? 0m : amount;

        /// <summary>RG-RM1–5 : rejette une remise hors bornes [0, 100].</summary>
        public static string? ValidateDiscountPercent(decimal discountPercent, string context) =>
            discountPercent < 0 || discountPercent > 100
                ? $"La remise ({context}) doit être comprise entre 0 et 100% (reçu : {discountPercent}%)."
                : null;

        /// <summary>RG-FA1 : frais de port HT ≥ 0.</summary>
        public static string? ValidateShippingAmount(decimal shippingAmountHt) =>
            shippingAmountHt < 0
                ? $"Les frais de port HT ne peuvent pas être négatifs (reçu : {shippingAmountHt})."
                : null;

        /// <summary>RG-CP3 : applique la remise pied de page sur HT/TVA déjà cumulés (proportionnelle, TTC recalculé).</summary>
        public static void ApplyHeaderDiscount(decimal headerDiscountPercent, ref decimal totalHT, ref decimal totalVat, ref decimal totalTTC)
        {
            if (headerDiscountPercent <= 0) return;
            var factor = 1 - (CapDiscountPercent(headerDiscountPercent) / 100m);
            totalHT *= factor;
            totalVat *= factor;
            totalTTC = totalHT + totalVat;
        }

        /// <summary>RG-CP1 : devise par défaut à la création d'un document, copiée de Company.DefaultCurrencyCode ("EUR" si absent).</summary>
        public static async System.Threading.Tasks.Task<string> ResolveCompanyCurrencyAsync(IStorageBroker storage, string? companyId)
        {
            var company = await storage.SelectCompanyByIdAsync(companyId);
            return string.IsNullOrWhiteSpace(company?.DefaultCurrencyCode) ? "EUR" : company!.DefaultCurrencyCode;
        }

        /// <summary>RG-CP1 : la devise est figée dès que le document quitte le statut Draft.</summary>
        public static string? RejectCurrencyChangeIfFrozen(string? existingStatus, string? existingCurrency, string? newCurrency)
        {
            if (string.IsNullOrWhiteSpace(existingStatus) || string.Equals(existingStatus, "Draft", StringComparison.OrdinalIgnoreCase))
                return null;
            var oldCode = string.IsNullOrWhiteSpace(existingCurrency) ? "EUR" : existingCurrency.Trim();
            var newCode = string.IsNullOrWhiteSpace(newCurrency) ? oldCode : newCurrency.Trim();
            if (string.Equals(oldCode, newCode, StringComparison.OrdinalIgnoreCase))
                return null;
            return $"Le code devise ({oldCode}) est figé une fois le document sorti du statut Draft (statut actuel : {existingStatus}).";
        }

        /// <summary>RG-T2 : un document enfant ne peut pas référencer un parent annulé/supprimé.</summary>
        public static string? RejectIfParentUnusable(string? parentStatus, string parentLabel)
        {
            if (string.IsNullOrWhiteSpace(parentStatus)) return null;
            if (string.Equals(parentStatus, "Cancelled", StringComparison.OrdinalIgnoreCase)
                || string.Equals(parentStatus, "Canceled", StringComparison.OrdinalIgnoreCase)
                || string.Equals(parentStatus, "Deleted", StringComparison.OrdinalIgnoreCase)
                || string.Equals(parentStatus, "Expired", StringComparison.OrdinalIgnoreCase)
                || string.Equals(parentStatus, "Rejected", StringComparison.OrdinalIgnoreCase)
                || string.Equals(parentStatus, "Refused", StringComparison.OrdinalIgnoreCase))
            {
                return $"Le document parent ({parentLabel}) est au statut {parentStatus} et ne peut plus être utilisé.";
            }
            return null;
        }

        /// <summary>RG-T3 / S2 : documents non brouillon non éditables (édition libre).</summary>
        public static bool CanFullyEdit(string? status) =>
            string.IsNullOrWhiteSpace(status)
            || string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Sent", StringComparison.OrdinalIgnoreCase);

        public static decimal GetLinkedCreditNotesTotal(
            IStorageBroker storage,
            int salesInvoiceId,
            int? excludeCreditNoteId = null)
        {
            return storage.SelectAllCreditNotes()
                .Where(c => c.SalesInvoiceId == salesInvoiceId
                    && c.Status != "Cancelled"
                    && (!excludeCreditNoteId.HasValue || c.Id != excludeCreditNoteId.Value))
                .AsEnumerable()
                .Sum(c => c.TotalTTC);
        }

        /// <summary>RG-A2 : Σ avoirs (hors Cancelled) ≤ TTC facture.</summary>
        public static string? ValidateCreditCap(
            IStorageBroker storage,
            SalesInvoice invoice,
            decimal additionalOrReplacementTotal,
            int? excludeCreditNoteId = null)
        {
            var existing = GetLinkedCreditNotesTotal(storage, invoice.Id, excludeCreditNoteId);
            var remainingCap = Math.Max(0m, invoice.TotalTTC - existing);
            var total = existing + additionalOrReplacementTotal;
            if (total > invoice.TotalTTC + 0.01m)
            {
                return $"Le total des avoirs ({total:0.##} €) dépasserait le TTC de la facture {invoice.InvoiceNumber} ({invoice.TotalTTC:0.##} €). "
                    + $"Avoirs déjà liés : {existing:0.##} €. Capacité restante : {remainingCap:0.##} €. "
                    + $"Montant demandé : {additionalOrReplacementTotal:0.##} €.";
            }
            return null;
        }

        /// <summary>RG-T6 : devis expiré non convertible.</summary>
        public static string? ValidateQuoteConvertible(Quote quote)
        {
            if (string.Equals(quote.Status, "Expired", StringComparison.OrdinalIgnoreCase)
                || string.Equals(quote.Status, "Refused", StringComparison.OrdinalIgnoreCase)
                || string.Equals(quote.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
            {
                return $"Le devis {quote.QuoteNumber} est au statut {quote.Status} et ne peut plus être converti.";
            }

            if (string.Equals(quote.Status, "Converted", StringComparison.OrdinalIgnoreCase))
            {
                return $"Le devis {quote.QuoteNumber} a déjà été entièrement converti.";
            }

            if (string.Equals(quote.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                return $"Le devis {quote.QuoteNumber} est annulé.";
            }

            // RG-DV3 : un devis Accepté ou PartiellementConverti peut générer une (nouvelle) commande.
            if (!string.Equals(quote.Status, "Accepted", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(quote.Status, "Accepté", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(quote.Status, "PartiallyConverted", StringComparison.OrdinalIgnoreCase))
            {
                return $"Le devis {quote.QuoteNumber} doit être Accepté avant conversion en commande (statut actuel : {quote.Status}).";
            }

            if (quote.ExpirationDate.Date < DateTime.UtcNow.Date)
            {
                return $"Le devis {quote.QuoteNumber} est expiré depuis le {quote.ExpirationDate:dd/MM/yyyy}.";
            }

            return null;
        }

        public static bool IsQuoteExpired(Quote quote) =>
            quote.ExpirationDate.Date < DateTime.UtcNow.Date
            && !string.Equals(quote.Status, "Converted", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(quote.Status, "Cancelled", StringComparison.OrdinalIgnoreCase);

        /// <summary>Commande engagée (post-confirm) : client et prix figés.</summary>
        public static bool IsOrderCommitted(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            return !string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>RG-BL1 : BL uniquement depuis une commande confirmée (pas Draft/Pending/Cancelled).</summary>
        public static string? RejectIfOrderNotConfirmedForDelivery(string? orderStatus)
        {
            if (string.Equals(orderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase)
                || string.Equals(orderStatus, "Draft", StringComparison.OrdinalIgnoreCase)
                || string.Equals(orderStatus, "Pending", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(orderStatus))
            {
                return $"La commande doit être confirmée avant création d'un BL (statut actuel : {orderStatus ?? "(vide)"}).";
            }

            return null;
        }

        /// <summary>RG-AC4 : avoir lié à un BRC → le retour doit être Integrated.</summary>
        public static string? RejectIfSalesReturnNotIntegrated(SalesReturn? salesReturn, int? salesReturnId)
        {
            if (!salesReturnId.HasValue || salesReturnId.Value <= 0) return null;
            if (salesReturn == null) return "Retour client (BRC) lié introuvable.";
            if (!string.Equals(salesReturn.Status, "Integrated", StringComparison.OrdinalIgnoreCase))
            {
                return $"Le retour client {salesReturn.ReturnNumber} doit être Intégré avant de valider/compenser cet avoir (statut actuel : {salesReturn.Status}).";
            }

            return null;
        }

        /// <summary>RG-BR2 : réception physique uniquement depuis Draft.</summary>
        public static string? RejectIfSalesReturnCannotReceive(string? status)
        {
            if (!string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase))
                return $"Retour au statut {status} — réception non applicable (Draft requis).";
            return null;
        }

        /// <summary>RG-BR3 : contrôle qualité uniquement après réception.</summary>
        public static string? RejectIfSalesReturnCannotControl(string? status)
        {
            if (!string.Equals(status, "Received", StringComparison.OrdinalIgnoreCase))
                return $"Retour au statut {status} — contrôle qualité non applicable (Received requis).";
            return null;
        }

        /// <summary>RG-BR4 : intégration après réception (pas Draft / Cancelled / déjà Integrated).</summary>
        public static string? RejectIfSalesReturnCannotIntegrate(string? status)
        {
            if (string.Equals(status, "Integrated", StringComparison.OrdinalIgnoreCase))
                return "Retour déjà intégré.";
            if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return "Un retour annulé ne peut pas être intégré.";
            if (string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(status))
            {
                return "Le retour doit être réceptionné (et idéalement contrôlé) avant intégration.";
            }

            return null;
        }

        /// <summary>RG-BR5 : annulation interdite une fois intégré.</summary>
        public static string? RejectIfSalesReturnCannotCancel(string? status)
        {
            if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return "Retour déjà annulé.";
            if (string.Equals(status, "Integrated", StringComparison.OrdinalIgnoreCase))
                return "Un retour déjà intégré ne peut pas être annulé.";
            return null;
        }

        /// <summary>RG-CC9 : client figé après confirmation.</summary>
        public static string? RejectIfCustomerChangedAfterCommit(string? status, int existingCustomerId, int newCustomerId)
        {
            if (IsOrderCommitted(status) && existingCustomerId != newCustomerId)
                return "Impossible de changer le client après confirmation de la commande.";
            return null;
        }

        /// <summary>RG-CC4/CC5 : ligne déjà livrée/facturée — pas de suppression, baisse sous livré, hausse, ni changement prix/TVA.</summary>
        public static string? RejectIfLockedOrderLineViolation(SalesOrderLine lockedLine, SalesOrderLine? match)
        {
            if (lockedLine.DeliveredQuantity <= 0 && lockedLine.InvoicedQuantity <= 0) return null;

            if (match == null)
                return $"Impossible de supprimer la ligne '{lockedLine.ProductKey}' déjà livrée/facturée.";
            if (!string.Equals(match.ProductKey?.Trim(), lockedLine.ProductKey?.Trim(), StringComparison.OrdinalIgnoreCase))
                return $"Impossible de changer l'article déjà livré/facturé '{lockedLine.ProductKey}'.";
            if (match.Quantity + 0.0001m < lockedLine.DeliveredQuantity)
                return $"Quantité de '{lockedLine.ProductKey}' inférieure à la qté déjà livrée ({lockedLine.DeliveredQuantity:0.####}).";
            if (match.Quantity + 0.0001m < lockedLine.InvoicedQuantity)
                return $"Quantité de '{lockedLine.ProductKey}' inférieure à la qté déjà facturée ({lockedLine.InvoicedQuantity:0.####}).";
            if (match.Quantity > lockedLine.Quantity + 0.0001m)
                return $"Impossible d'augmenter la quantité de '{lockedLine.ProductKey}' déjà partiellement livrée.";
            if (Math.Abs(match.UnitPrice - lockedLine.UnitPrice) > 0.0001m
                || Math.Abs(match.VatRate - lockedLine.VatRate) > 0.0001m)
            {
                return $"Prix/TVA de '{lockedLine.ProductKey}' figés (ligne déjà livrée/facturée).";
            }

            return null;
        }

        /// <summary>RG-T3 : même client sur le flux.</summary>
        public static string? ValidateSameCustomer(int expectedCustomerId, int actualCustomerId, string context)
        {
            if (expectedCustomerId != actualCustomerId)
                return $"Le client doit être identique sur tout le flux ({context}).";
            return null;
        }

        /// <summary>RG-T7 : documents non brouillon non suppressibles (hard delete).</summary>
        public static bool CanPhysicallyDelete(string? status) =>
            string.IsNullOrWhiteSpace(status)
            || string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase);

        /// <summary>P3 soft-delete : brouillons uniquement (les validés passent par annulation).</summary>
        public static void SoftDelete(IHasSoftDelete entity, string? actor)
        {
            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;
            entity.DeletedBy = string.IsNullOrWhiteSpace(actor) ? "System" : actor.Trim();
        }

        /// <summary>Corbeille : restauration d'un brouillon soft-supprimé (redevient visible / éditable).</summary>
        public static bool CanRestoreSoftDeleted(string? status) => CanPhysicallyDelete(status);

        public static void RestoreSoftDelete(IHasSoftDelete entity)
        {
            entity.IsDeleted = false;
            entity.DeletedAt = null;
            entity.DeletedBy = null;
        }

        /// <summary>P4 archivage : documents clôturés / annulés hors listes actives.</summary>
        public static bool CanArchive(string? status) =>
            string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Invoiced", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Delivered", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Converted", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Applied", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Paid", StringComparison.OrdinalIgnoreCase);

        public static void Archive(IHasArchive entity, string? actor)
        {
            entity.IsArchived = true;
            entity.ArchivedAt = DateTime.UtcNow;
            entity.ArchivedBy = string.IsNullOrWhiteSpace(actor) ? "System" : actor.Trim();
        }

        public static bool IsPendingStatus(string? status) =>
            string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase);

        public static decimal RemainingQuantity(SalesOrderLine line) =>
            Math.Max(0m, line.Quantity - line.DeliveredQuantity);

        /// <summary>
        /// Quantité déjà engagée sur des BL non annulés (Draft inclus),
        /// pour éviter plusieurs BL sur les mêmes quantités avant validation.
        /// </summary>
        public static decimal AllocatedOnDeliveryNotes(
            IStorageBroker storage,
            int salesOrderId,
            string? productKey)
        {
            var key = (productKey ?? "").Trim();
            return storage.SelectAllSalesDeliveryNotes()
                .Where(n => n.SalesOrderId == salesOrderId)
                .AsEnumerable()
                .Where(n => !string.Equals(n.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                .SelectMany(n => n.Lines ?? new System.Collections.Generic.List<SalesDeliveryNoteLine>())
                .Where(l => string.Equals((l.ProductKey ?? "").Trim(), key, StringComparison.OrdinalIgnoreCase))
                .Sum(l => l.DeliveredQuantity);
        }

        public static decimal RemainingToShip(
            IStorageBroker storage,
            SalesOrder order,
            SalesOrderLine line)
        {
            var allocated = AllocatedOnDeliveryNotes(storage, order.Id, line.ProductKey);
            return Math.Max(0m, line.Quantity - allocated);
        }

        public static bool HasRemainingToDeliver(SalesOrder order) =>
            (order.Lines ?? new System.Collections.Generic.List<SalesOrderLine>())
                .Any(l => RemainingQuantity(l) > 0.0001m);

        public static bool HasOpenQuantityToShip(IStorageBroker storage, SalesOrder order) =>
            (order.Lines ?? new System.Collections.Generic.List<SalesOrderLine>())
                .Any(l => RemainingToShip(storage, order, l) > 0.0001m);

        /// <summary>RG-V8 : maj statut commande selon qty livrée / facturée.</summary>
        public static void RefreshOrderFulfillmentStatus(SalesOrder order)
        {
            if (string.Equals(order.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return;
            // Pending = en attente crédit/validation : ne pas écraser par le suivi livraison.
            if (IsPendingStatus(order.Status))
                return;

            var lines = order.Lines ?? new System.Collections.Generic.List<SalesOrderLine>();
            if (lines.Count == 0) return;

            var allDelivered = lines.All(l => l.DeliveredQuantity >= l.Quantity - 0.0001m);
            var allInvoiced = lines.All(l => l.InvoicedQuantity >= l.Quantity - 0.0001m);
            var anyDelivered = lines.Any(l => l.DeliveredQuantity > 0);
            var anyInvoiced = lines.Any(l => l.InvoicedQuantity > 0);

            if (allDelivered && allInvoiced)
                order.Status = "Closed";
            else if (allInvoiced)
                order.Status = "Invoiced";
            else if (allDelivered)
                order.Status = "Delivered";
            else if (anyDelivered)
                order.Status = "PartiallyDelivered";
            else if (anyInvoiced)
                order.Status = "PartiallyInvoiced";
        }

        public static void AppendNote(SalesOrder order, string note)
        {
            if (string.IsNullOrWhiteSpace(note)) return;
            order.Notes = string.IsNullOrWhiteSpace(order.Notes)
                ? note.Trim()
                : $"{order.Notes.TrimEnd()}\n{note.Trim()}";
        }

        public static void AddInvoicedQuantities(SalesOrder order, System.Collections.Generic.IEnumerable<(string ProductKey, decimal Qty)> lines)
        {
            foreach (var (key, qty) in lines)
            {
                var line = order.Lines.FirstOrDefault(l =>
                    string.Equals(l.ProductKey?.Trim(), key?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (line != null)
                    line.InvoicedQuantity = Math.Min(line.Quantity, line.InvoicedQuantity + qty);
            }
            RefreshOrderFulfillmentStatus(order);
        }

        /// <summary>Ajuste les qté facturées (delta positif ou négatif), ex. édition d'une facture Draft.</summary>
        public static void AdjustInvoicedQuantities(SalesOrder order, System.Collections.Generic.IEnumerable<(string ProductKey, decimal Delta)> deltas)
        {
            foreach (var (key, delta) in deltas)
            {
                if (delta == 0m) continue;
                var line = order.Lines.FirstOrDefault(l =>
                    string.Equals(l.ProductKey?.Trim(), key?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (line == null) continue;
                line.InvoicedQuantity = Math.Max(0m, Math.Min(line.Quantity, line.InvoicedQuantity + delta));
            }
            RefreshOrderFulfillmentStatus(order);
        }

        /// <summary>RG-T5 : encours + commandes ouvertes + nouvelle commande ≤ plafond (0 = illimité).</summary>
        public static string? ValidateCreditLimit(
            IStorageBroker storage,
            Customer customer,
            decimal additionalOrderTtc,
            int? excludeOrderId = null)
        {
            if (customer.CreditLimit <= 0) return null;

            var openOrders = storage.SelectAllSalesOrders()
                .Where(o => o.CustomerId == customer.Id
                    && (!excludeOrderId.HasValue || o.Id != excludeOrderId.Value)
                    && o.Status != "Cancelled"
                    && o.Status != "Draft"
                    && o.Status != "Closed")
                .AsEnumerable()
                .Sum(o => o.TotalTTC);

            var projected = customer.Balance + openOrders + additionalOrderTtc;
            if (projected > customer.CreditLimit + 0.01m)
            {
                return $"Plafond de crédit dépassé pour {customer.Name} : engagé {projected:0.##} € / plafond {customer.CreditLimit:0.##} €.";
            }

            return null;
        }
    }
}
