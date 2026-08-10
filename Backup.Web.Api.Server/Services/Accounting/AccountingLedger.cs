using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Services.Numbering;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Services.Accounting
{
    /// <summary>Poste les écritures PCG simplifiées (411/701/44571, 401/607/44566).</summary>
    public static class AccountingLedger
    {
        public const string RefSalesInvoice = "SalesInvoice";
        public const string RefCreditNote = "CreditNote";
        public const string RefSupplierInvoice = "SupplierInvoice";
        public const string RefSupplierCreditNote = "SupplierCreditNote";
        public const string RefPayment = "SalesPayment";
        public const string RefPaymentReversal = "SalesPaymentReversal";
        public const string RefSupplierPayment = "SupplierPayment";
        public const string RefDepositInvoice = "DepositInvoice";
        public const string RefDepositApplication = "DepositApplication";
        public const string RefDepositCancellation = "DepositCancellation";

        public static bool HasPostedEntry(IStorageBroker storage, string referenceType, int referenceId, string? companyId)
        {
            return storage.SelectAllAccountingEntries()
                .ForCompany(companyId)
                .Any(e => e.ReferenceType == referenceType && e.ReferenceId == referenceId && e.Status == "Posted");
        }

        public static async Task<(AccountingEntry? Entry, string? Error)> PostSalesInvoiceAsync(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            SalesInvoice invoice,
            string? createdBy)
        {
            if (invoice.TotalTTC <= 0) return (null, "Montant facture invalide pour écriture comptable.");
            if (HasPostedEntry(storage, RefSalesInvoice, invoice.Id, invoice.CompanyId))
                return (null, "Écriture déjà postée pour cette facture.");

            var customer = await storage.SelectCustomerByIdAsync(invoice.CustomerId);
            if (customer == null) return (null, "Client introuvable.");

            var (entry, error) = await CreateEntryAsync(
                storage,
                numbering,
                invoice.CompanyId,
                "SalesInvoice",
                RefSalesInvoice,
                invoice.Id,
                $"Vente {invoice.InvoiceNumber}",
                createdBy,
                new[]
                {
                    Line("411000", $"Clients — {customer.Name}", invoice.TotalTTC, 0),
                    Line("701000", "Ventes de marchandises", 0, invoice.TotalHT),
                    Line("445710", "TVA collectée", 0, invoice.TotalVat)
                });
            if (error != null) return (null, error);

            customer.Balance += invoice.TotalTTC;
            customer.UpdatedAt = DateTime.UtcNow;
            await storage.UpdateCustomerAsync(customer);

            return (entry, null);
        }

        public static async Task<(AccountingEntry? Entry, string? Error)> PostCreditNoteAsync(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            CreditNoteEntity creditNote,
            string? createdBy)
        {
            if (creditNote.TotalTTC <= 0) return (null, "Montant avoir invalide pour écriture comptable.");
            if (HasPostedEntry(storage, RefCreditNote, creditNote.Id, creditNote.CompanyId))
                return (null, "Écriture déjà postée pour cet avoir.");

            var customer = await storage.SelectCustomerByIdAsync(creditNote.CustomerId);
            if (customer == null) return (null, "Client introuvable.");

            // Inverse vente : débit ventes/TVA, crédit client (Balance déjà diminué côté Apply)
            return await CreateEntryAsync(
                storage,
                numbering,
                creditNote.CompanyId,
                "CreditNote",
                RefCreditNote,
                creditNote.Id,
                $"Avoir {creditNote.CreditNoteNumber}",
                createdBy,
                new[]
                {
                    Line("701000", "Ventes de marchandises (avoir)", creditNote.TotalHT, 0),
                    Line("445710", "TVA collectée (avoir)", creditNote.TotalVat, 0),
                    Line("411000", $"Clients — {customer.Name}", 0, creditNote.TotalTTC)
                });
        }

        public static async Task<(AccountingEntry? Entry, string? Error)> PostSupplierInvoiceAsync(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            SupplierInvoiceEntity invoice,
            string? createdBy)
        {
            if (invoice.TotalTTC <= 0) return (null, "Montant facture fournisseur invalide.");
            if (HasPostedEntry(storage, RefSupplierInvoice, invoice.Id, invoice.CompanyId))
                return (null, "Écriture déjà postée pour cette facture fournisseur.");

            var supplier = await storage.SelectSupplierByIdAsync(invoice.SupplierId);
            if (supplier == null) return (null, "Fournisseur introuvable.");

            var (entry, error) = await CreateEntryAsync(
                storage,
                numbering,
                invoice.CompanyId,
                "SupplierInvoice",
                RefSupplierInvoice,
                invoice.Id,
                $"Achat {invoice.InvoiceNumber}",
                createdBy,
                new[]
                {
                    Line("607000", "Achats de marchandises", invoice.TotalHT, 0),
                    Line("445660", "TVA déductible", invoice.TotalVat, 0),
                    Line("401000", $"Fournisseurs — {supplier.Name}", 0, invoice.TotalTTC)
                });
            if (error != null) return (null, error);

            supplier.Balance += invoice.TotalTTC;
            supplier.UpdatedAt = DateTime.UtcNow;
            await storage.UpdateSupplierAsync(supplier);

            return (entry, null);
        }

        /// <summary>RG-AF3 : écriture inverse de la facture fournisseur (débit 401, crédit 607/44566).</summary>
        public static async Task<(AccountingEntry? Entry, string? Error)> PostSupplierCreditNoteAsync(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            SupplierCreditNoteEntity creditNote,
            string? createdBy)
        {
            if (creditNote.TotalTTC <= 0) return (null, "Montant avoir fournisseur invalide pour écriture comptable.");
            if (HasPostedEntry(storage, RefSupplierCreditNote, creditNote.Id, creditNote.CompanyId))
                return (null, "Écriture déjà postée pour cet avoir fournisseur.");

            var supplier = await storage.SelectSupplierByIdAsync(creditNote.SupplierId);
            if (supplier == null) return (null, "Fournisseur introuvable.");

            return await CreateEntryAsync(
                storage,
                numbering,
                creditNote.CompanyId,
                "SupplierCreditNote",
                RefSupplierCreditNote,
                creditNote.Id,
                $"Avoir fournisseur {creditNote.CreditNoteNumber}",
                createdBy,
                new[]
                {
                    Line("401000", $"Fournisseurs — {supplier.Name}", creditNote.TotalTTC, 0),
                    Line("607000", "Achats de marchandises (avoir)", 0, creditNote.TotalHT),
                    Line("445660", "TVA déductible (avoir)", 0, creditNote.TotalVat)
                });
        }

        /// <summary>Réduit l'encours client lors d'un règlement. ReferenceId = Payment.Id (RG-CO4).</summary>
        public static async Task<(AccountingEntry? Entry, string? Error)> PostSalesPaymentAsync(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            SalesInvoice invoice,
            Payment payment,
            string? createdBy)
        {
            if (payment.Amount <= 0) return (null, null);
            if (HasPostedEntry(storage, RefPayment, payment.Id, payment.CompanyId ?? invoice.CompanyId))
                return (null, "Écriture déjà postée pour ce paiement.");

            var customer = await storage.SelectCustomerByIdAsync(invoice.CustomerId);
            if (customer == null) return (null, "Client introuvable.");

            var method = payment.Method ?? "Transfer";
            var cashAccount = string.Equals(method, "Cash", StringComparison.OrdinalIgnoreCase) ? "530000" : "512000";
            var cashLabel = string.Equals(method, "Cash", StringComparison.OrdinalIgnoreCase) ? "Caisse" : "Banque";

            var (entry, error) = await CreateEntryAsync(
                storage,
                numbering,
                invoice.CompanyId,
                "Payment",
                RefPayment,
                payment.Id,
                $"Règlement {invoice.InvoiceNumber} ({method}) {payment.Amount:0.##}",
                createdBy,
                new[]
                {
                    Line(cashAccount, cashLabel, payment.Amount, 0),
                    Line("411000", $"Clients — {customer.Name}", 0, payment.Amount)
                });
            if (error != null) return (null, error);

            customer.Balance = Math.Max(0, customer.Balance - payment.Amount);
            customer.UpdatedAt = DateTime.UtcNow;
            await storage.UpdateCustomerAsync(customer);

            return (entry, null);
        }

        /// <summary>Règlement fournisseur : débit 401, crédit banque/caisse. ReferenceId = SupplierPayment.Id.</summary>
        public static async Task<(AccountingEntry? Entry, string? Error)> PostSupplierPaymentAsync(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            SupplierInvoiceEntity invoice,
            SupplierPayment payment,
            string? createdBy)
        {
            if (payment.Amount <= 0) return (null, null);
            if (HasPostedEntry(storage, RefSupplierPayment, payment.Id, payment.CompanyId ?? invoice.CompanyId))
                return (null, "Écriture déjà postée pour ce paiement fournisseur.");

            var supplier = await storage.SelectSupplierByIdAsync(invoice.SupplierId);
            if (supplier == null) return (null, "Fournisseur introuvable.");

            var method = payment.Method ?? "BankTransfer";
            var cashAccount = string.Equals(method, "Cash", StringComparison.OrdinalIgnoreCase) ? "530000" : "512000";
            var cashLabel = string.Equals(method, "Cash", StringComparison.OrdinalIgnoreCase) ? "Caisse" : "Banque";

            var (entry, error) = await CreateEntryAsync(
                storage,
                numbering,
                invoice.CompanyId,
                "SupplierPayment",
                RefSupplierPayment,
                payment.Id,
                $"Règlement fournisseur {invoice.InvoiceNumber} ({method}) {payment.Amount:0.##}",
                createdBy,
                new[]
                {
                    Line("401000", $"Fournisseurs — {supplier.Name}", payment.Amount, 0),
                    Line(cashAccount, cashLabel, 0, payment.Amount)
                });
            if (error != null) return (null, error);

            supplier.Balance = Math.Max(0, supplier.Balance - payment.Amount);
            supplier.UpdatedAt = DateTime.UtcNow;
            await storage.UpdateSupplierAsync(supplier);

            return (entry, null);
        }

        /// <summary>RG-CO4 : écriture inverse à l'annulation d'un paiement (lettrage figé → reverse).</summary>
        public static async Task<(AccountingEntry? Entry, string? Error)> ReverseSalesPaymentAsync(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            Payment payment,
            SalesInvoice invoice,
            string? createdBy)
        {
            if (payment.Amount <= 0) return (null, null);
            if (HasPostedEntry(storage, RefPaymentReversal, payment.Id, payment.CompanyId ?? invoice.CompanyId))
                return (null, null);

            var customer = await storage.SelectCustomerByIdAsync(invoice.CustomerId);
            if (customer == null) return (null, "Client introuvable.");

            var method = payment.Method ?? "Transfer";
            var cashAccount = string.Equals(method, "Cash", StringComparison.OrdinalIgnoreCase) ? "530000" : "512000";
            var cashLabel = string.Equals(method, "Cash", StringComparison.OrdinalIgnoreCase) ? "Caisse" : "Banque";

            return await CreateEntryAsync(
                storage,
                numbering,
                invoice.CompanyId,
                "PaymentReversal",
                RefPaymentReversal,
                payment.Id,
                $"Annulation règlement {invoice.InvoiceNumber} ({method}) {payment.Amount:0.##}",
                createdBy,
                new[]
                {
                    Line("411000", $"Clients — {customer.Name}", payment.Amount, 0),
                    Line(cashAccount, cashLabel, 0, payment.Amount)
                });
        }

        /// <summary>RG-AA2 : validation acompte — Débit 411 (client), Crédit 419 (avances/acomptes reçus), simplifié TTC.</summary>
        public static async Task<(AccountingEntry? Entry, string? Error)> PostDepositInvoiceAsync(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            DepositInvoice deposit,
            string? createdBy)
        {
            if (deposit.AmountTTC <= 0) return (null, "Montant acompte invalide pour écriture comptable.");
            if (HasPostedEntry(storage, RefDepositInvoice, deposit.Id, deposit.CompanyId))
                return (null, "Écriture déjà postée pour cet acompte.");

            var customer = await storage.SelectCustomerByIdAsync(deposit.CustomerId);
            if (customer == null) return (null, "Client introuvable.");

            var (entry, error) = await CreateEntryAsync(
                storage,
                numbering,
                deposit.CompanyId,
                "DepositInvoice",
                RefDepositInvoice,
                deposit.Id,
                $"Acompte {deposit.DepositNumber}",
                createdBy,
                new[]
                {
                    Line("411000", $"Clients — {customer.Name}", deposit.AmountTTC, 0),
                    Line("419000", "Avances et acomptes reçus sur commandes", 0, deposit.AmountTTC)
                });
            if (error != null) return (null, error);

            customer.Balance += deposit.AmountTTC;
            customer.UpdatedAt = DateTime.UtcNow;
            await storage.UpdateCustomerAsync(customer);

            return (entry, null);
        }

        /// <summary>RG-AA3 : application de l'acompte sur la facture finale — reverse 419/411 (ne repasse pas par le compte de vente).</summary>
        public static async Task<(AccountingEntry? Entry, string? Error)> PostDepositApplicationAsync(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            DepositInvoice deposit,
            SalesInvoice invoice,
            string? createdBy)
        {
            if (deposit.AmountTTC <= 0) return (null, null);
            if (HasPostedEntry(storage, RefDepositApplication, deposit.Id, deposit.CompanyId))
                return (null, "Cet acompte a déjà été appliqué.");

            var customer = await storage.SelectCustomerByIdAsync(deposit.CustomerId);
            if (customer == null) return (null, "Client introuvable.");

            var (entry, error) = await CreateEntryAsync(
                storage,
                numbering,
                deposit.CompanyId,
                "DepositApplication",
                RefDepositApplication,
                deposit.Id,
                $"Application acompte {deposit.DepositNumber} sur facture {invoice.InvoiceNumber}",
                createdBy,
                new[]
                {
                    Line("419000", "Avances et acomptes reçus sur commandes", deposit.AmountTTC, 0),
                    Line("411000", $"Clients — {customer.Name}", 0, deposit.AmountTTC)
                });
            if (error != null) return (null, error);

            customer.Balance = Math.Max(0, customer.Balance - deposit.AmountTTC);
            customer.UpdatedAt = DateTime.UtcNow;
            await storage.UpdateCustomerAsync(customer);

            return (entry, null);
        }

        /// <summary>RG-AA4 : annulation d'un acompte Validated (jamais appliqué) — reverse 419/411, restitue le compte client.</summary>
        public static async Task<(AccountingEntry? Entry, string? Error)> PostDepositCancellationAsync(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            DepositInvoice deposit,
            string? createdBy)
        {
            if (deposit.AmountTTC <= 0) return (null, null);
            if (HasPostedEntry(storage, RefDepositCancellation, deposit.Id, deposit.CompanyId))
                return (null, null);

            var customer = await storage.SelectCustomerByIdAsync(deposit.CustomerId);
            if (customer == null) return (null, "Client introuvable.");

            var (entry, error) = await CreateEntryAsync(
                storage,
                numbering,
                deposit.CompanyId,
                "DepositCancellation",
                RefDepositCancellation,
                deposit.Id,
                $"Annulation acompte {deposit.DepositNumber}",
                createdBy,
                new[]
                {
                    Line("419000", "Avances et acomptes reçus sur commandes", deposit.AmountTTC, 0),
                    Line("411000", $"Clients — {customer.Name}", 0, deposit.AmountTTC)
                });
            if (error != null) return (null, error);

            customer.Balance = Math.Max(0, customer.Balance - deposit.AmountTTC);
            customer.UpdatedAt = DateTime.UtcNow;
            await storage.UpdateCustomerAsync(customer);

            return (entry, null);
        }

        /// <summary>RG-CO3 : date comptable comprise dans l'exercice ouvert [début, fin].</summary>
        public static async Task<string?> ValidateOpenFiscalPeriodAsync(
            IStorageBroker storage,
            string? companyId,
            DateTime entryDate)
        {
            if (string.IsNullOrWhiteSpace(companyId)) return null;
            var company = await storage.SelectCompanyByIdAsync(companyId);
            if (company == null) return null;

            if (company.OpenFiscalPeriodStart != null)
            {
                var start = company.OpenFiscalPeriodStart.Value.Date;
                if (entryDate.Date < start)
                {
                    return $"La date comptable ({entryDate:dd/MM/yyyy}) est antérieure au début de l'exercice ouvert ({start:dd/MM/yyyy}).";
                }
            }

            if (company.OpenFiscalPeriodEnd != null)
            {
                var end = company.OpenFiscalPeriodEnd.Value.Date;
                if (entryDate.Date > end)
                {
                    return $"La date comptable ({entryDate:dd/MM/yyyy}) est postérieure à la fin de l'exercice ouvert ({end:dd/MM/yyyy}).";
                }
            }

            return null;
        }

        private static async Task<(AccountingEntry? Entry, string? Error)> CreateEntryAsync(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            string? companyId,
            string journalType,
            string referenceType,
            int referenceId,
            string description,
            string? createdBy,
            IEnumerable<AccountingEntryLine> lines)
        {
            var entryDate = DateTime.UtcNow;
            var fiscalError = await ValidateOpenFiscalPeriodAsync(storage, companyId, entryDate);
            if (fiscalError != null) return (null, fiscalError);

            var entry = new AccountingEntry
            {
                EntryNumber = await numbering.GetNextNumberAsync("AccountingEntry", companyId),
                EntryDate = entryDate,
                JournalType = journalType,
                ReferenceType = referenceType,
                ReferenceId = referenceId,
                Description = description,
                Status = "Posted",
                CompanyId = companyId,
                // Si un GUID a été passé par erreur, laisser vide → ApplyAuditTrail mettra le nom affiché.
                CreatedBy = SalesDocumentAudit.IsReadableActor(createdBy) ? createdBy!.Trim() : null,
                CreatedAt = DateTime.UtcNow,
                Lines = lines.ToList()
            };
            Renumber(entry.Lines);
            var saved = await storage.InsertAccountingEntryAsync(entry);
            return (saved, null);
        }

        private static AccountingEntryLine Line(string code, string label, decimal debit, decimal credit) =>
            new()
            {
                AccountCode = code,
                AccountLabel = label,
                Debit = Math.Round(debit, 4),
                Credit = Math.Round(credit, 4)
            };

        private static void Renumber(List<AccountingEntryLine> lines)
        {
            for (var i = 0; i < lines.Count; i++)
                lines[i].LineNumber = i + 1;
        }
    }
}
