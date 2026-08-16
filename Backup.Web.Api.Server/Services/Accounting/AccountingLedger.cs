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
    /// <summary>
    /// Poste les écritures comptables automatiques (ventes, achats, règlements, acomptes).
    /// Les comptes proviennent des paramètres comptables de la société (Phase 2) — à défaut,
    /// les comptes PCG historiques (411/701/44571, 401/607/44566, 419, 512, 530) sont appliqués.
    /// La TVA est ventilée par taux (mapping CompanyVatRateAccount), et l'écriture est rattachée
    /// au journal structuré (ACH/VEN/BAN/CAIS) et à la période fiscale quand ils existent.
    /// </summary>
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

        // Codes des journaux structurés (seed Phase 1) utilisés pour renseigner JournalId.
        private const string JournalCodeVentes = "VEN";
        private const string JournalCodeAchats = "ACH";
        private const string JournalCodeBanque = "BAN";
        private const string JournalCodeCaisse = "CAIS";

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

            var settings = await AccountingEntryResolver.ResolveSettingsAsync(storage, invoice.CompanyId);
            var lines = new List<AccountingEntryLine>
            {
                Line(settings.CustomerAccountCode, $"Clients — {customer.Name}", invoice.TotalTTC, 0),
                Line(settings.SalesAccountCode, "Ventes de marchandises", 0, invoice.TotalHT)
            };
            lines.AddRange(await BuildVatLinesAsync(
                storage,
                invoice.CompanyId,
                DocumentVatSources(invoice.Lines, l => l.VatRate, l => l.TotalTTC - l.TotalHT,
                    invoice.ShippingAmountHt, invoice.ShippingVatRate),
                invoice.TotalVat,
                isCollected: true,
                label: "TVA collectée",
                debitSide: false));

            var (entry, error) = await CreateEntryAsync(
                storage,
                numbering,
                invoice.CompanyId,
                "SalesInvoice",
                JournalCodeVentes,
                RefSalesInvoice,
                invoice.Id,
                $"Vente {invoice.InvoiceNumber}",
                createdBy,
                lines);
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

            var settings = await AccountingEntryResolver.ResolveSettingsAsync(storage, creditNote.CompanyId);
            var lines = new List<AccountingEntryLine>
            {
                Line(settings.SalesAccountCode, "Ventes de marchandises (avoir)", creditNote.TotalHT, 0)
            };
            lines.AddRange(await BuildVatLinesAsync(
                storage,
                creditNote.CompanyId,
                DocumentVatSources(creditNote.Lines, l => l.VatRate, l => l.TotalTTC - l.TotalHT, 0m, 0m),
                creditNote.TotalVat,
                isCollected: true,
                label: "TVA collectée (avoir)",
                debitSide: true));
            // Inverse vente : débit ventes/TVA, crédit client (Balance déjà diminué côté Apply)
            lines.Add(Line(settings.CustomerAccountCode, $"Clients — {customer.Name}", 0, creditNote.TotalTTC));

            return await CreateEntryAsync(
                storage,
                numbering,
                creditNote.CompanyId,
                "CreditNote",
                JournalCodeVentes,
                RefCreditNote,
                creditNote.Id,
                $"Avoir {creditNote.CreditNoteNumber}",
                createdBy,
                lines);
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

            var settings = await AccountingEntryResolver.ResolveSettingsAsync(storage, invoice.CompanyId);
            var lines = new List<AccountingEntryLine>
            {
                Line(settings.PurchaseAccountCode, "Achats de marchandises", invoice.TotalHT, 0)
            };
            lines.AddRange(await BuildVatLinesAsync(
                storage,
                invoice.CompanyId,
                DocumentVatSources(invoice.Lines, l => l.VatRate, l => l.TotalTTC - l.TotalHT,
                    invoice.ShippingAmountHt, invoice.ShippingVatRate),
                invoice.TotalVat,
                isCollected: false,
                label: "TVA déductible",
                debitSide: true));
            lines.Add(Line(settings.SupplierAccountCode, $"Fournisseurs — {supplier.Name}", 0, invoice.TotalTTC));

            var (entry, error) = await CreateEntryAsync(
                storage,
                numbering,
                invoice.CompanyId,
                "SupplierInvoice",
                JournalCodeAchats,
                RefSupplierInvoice,
                invoice.Id,
                $"Achat {invoice.InvoiceNumber}",
                createdBy,
                lines);
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

            var settings = await AccountingEntryResolver.ResolveSettingsAsync(storage, creditNote.CompanyId);
            var lines = new List<AccountingEntryLine>
            {
                Line(settings.SupplierAccountCode, $"Fournisseurs — {supplier.Name}", creditNote.TotalTTC, 0),
                Line(settings.PurchaseAccountCode, "Achats de marchandises (avoir)", 0, creditNote.TotalHT)
            };
            lines.AddRange(await BuildVatLinesAsync(
                storage,
                creditNote.CompanyId,
                DocumentVatSources(creditNote.Lines, l => l.VatRate, l => l.TotalTTC - l.TotalHT, 0m, 0m),
                creditNote.TotalVat,
                isCollected: false,
                label: "TVA déductible (avoir)",
                debitSide: false));

            return await CreateEntryAsync(
                storage,
                numbering,
                creditNote.CompanyId,
                "SupplierCreditNote",
                JournalCodeAchats,
                RefSupplierCreditNote,
                creditNote.Id,
                $"Avoir fournisseur {creditNote.CreditNoteNumber}",
                createdBy,
                lines);
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

            var settings = await AccountingEntryResolver.ResolveSettingsAsync(storage, invoice.CompanyId);
            var method = payment.Method ?? "Transfer";
            var isCash = string.Equals(method, "Cash", StringComparison.OrdinalIgnoreCase);
            var cashAccount = isCash ? settings.CashAccountCode : settings.BankAccountCode;
            var cashLabel = isCash ? "Caisse" : "Banque";

            var (entry, error) = await CreateEntryAsync(
                storage,
                numbering,
                invoice.CompanyId,
                "Payment",
                isCash ? JournalCodeCaisse : JournalCodeBanque,
                RefPayment,
                payment.Id,
                $"Règlement {invoice.InvoiceNumber} ({method}) {payment.Amount:0.##}",
                createdBy,
                new[]
                {
                    Line(cashAccount, cashLabel, payment.Amount, 0),
                    Line(settings.CustomerAccountCode, $"Clients — {customer.Name}", 0, payment.Amount)
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

            var settings = await AccountingEntryResolver.ResolveSettingsAsync(storage, invoice.CompanyId);
            var method = payment.Method ?? "BankTransfer";
            var isCash = string.Equals(method, "Cash", StringComparison.OrdinalIgnoreCase);
            var cashAccount = isCash ? settings.CashAccountCode : settings.BankAccountCode;
            var cashLabel = isCash ? "Caisse" : "Banque";

            var (entry, error) = await CreateEntryAsync(
                storage,
                numbering,
                invoice.CompanyId,
                "SupplierPayment",
                isCash ? JournalCodeCaisse : JournalCodeBanque,
                RefSupplierPayment,
                payment.Id,
                $"Règlement fournisseur {invoice.InvoiceNumber} ({method}) {payment.Amount:0.##}",
                createdBy,
                new[]
                {
                    Line(settings.SupplierAccountCode, $"Fournisseurs — {supplier.Name}", payment.Amount, 0),
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

            var settings = await AccountingEntryResolver.ResolveSettingsAsync(storage, invoice.CompanyId);
            var method = payment.Method ?? "Transfer";
            var isCash = string.Equals(method, "Cash", StringComparison.OrdinalIgnoreCase);
            var cashAccount = isCash ? settings.CashAccountCode : settings.BankAccountCode;
            var cashLabel = isCash ? "Caisse" : "Banque";

            return await CreateEntryAsync(
                storage,
                numbering,
                invoice.CompanyId,
                "PaymentReversal",
                isCash ? JournalCodeCaisse : JournalCodeBanque,
                RefPaymentReversal,
                payment.Id,
                $"Annulation règlement {invoice.InvoiceNumber} ({method}) {payment.Amount:0.##}",
                createdBy,
                new[]
                {
                    Line(settings.CustomerAccountCode, $"Clients — {customer.Name}", payment.Amount, 0),
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

            var settings = await AccountingEntryResolver.ResolveSettingsAsync(storage, deposit.CompanyId);
            var (entry, error) = await CreateEntryAsync(
                storage,
                numbering,
                deposit.CompanyId,
                "DepositInvoice",
                JournalCodeVentes,
                RefDepositInvoice,
                deposit.Id,
                $"Acompte {deposit.DepositNumber}",
                createdBy,
                new[]
                {
                    Line(settings.CustomerAccountCode, $"Clients — {customer.Name}", deposit.AmountTTC, 0),
                    Line(settings.CustomerDepositAccountCode, "Avances et acomptes reçus sur commandes", 0, deposit.AmountTTC)
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

            var settings = await AccountingEntryResolver.ResolveSettingsAsync(storage, deposit.CompanyId);
            var (entry, error) = await CreateEntryAsync(
                storage,
                numbering,
                deposit.CompanyId,
                "DepositApplication",
                JournalCodeVentes,
                RefDepositApplication,
                deposit.Id,
                $"Application acompte {deposit.DepositNumber} sur facture {invoice.InvoiceNumber}",
                createdBy,
                new[]
                {
                    Line(settings.CustomerDepositAccountCode, "Avances et acomptes reçus sur commandes", deposit.AmountTTC, 0),
                    Line(settings.CustomerAccountCode, $"Clients — {customer.Name}", 0, deposit.AmountTTC)
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

            var settings = await AccountingEntryResolver.ResolveSettingsAsync(storage, deposit.CompanyId);
            var (entry, error) = await CreateEntryAsync(
                storage,
                numbering,
                deposit.CompanyId,
                "DepositCancellation",
                JournalCodeVentes,
                RefDepositCancellation,
                deposit.Id,
                $"Annulation acompte {deposit.DepositNumber}",
                createdBy,
                new[]
                {
                    Line(settings.CustomerDepositAccountCode, "Avances et acomptes reçus sur commandes", deposit.AmountTTC, 0),
                    Line(settings.CustomerAccountCode, $"Clients — {customer.Name}", 0, deposit.AmountTTC)
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
            string journalCode,
            string referenceType,
            int referenceId,
            string description,
            string? createdBy,
            IEnumerable<AccountingEntryLine> lines)
        {
            var entryDate = DateTime.UtcNow;
            // Phase 2 : exercice/période structurés si la société en a, sinon bornes legacy (RG-CO3).
            var period = await AccountingEntryResolver.ResolvePeriodAsync(storage, companyId, entryDate);
            if (period.Error != null) return (null, period.Error);

            // Journal structuré (null si absent : ne jamais bloquer la génération pour ça).
            var journal = await AccountingEntryResolver.ResolveJournalAsync(storage, companyId, journalCode);

            var entry = new AccountingEntry
            {
                EntryNumber = await numbering.GetNextNumberAsync("AccountingEntry", companyId),
                EntryDate = entryDate,
                JournalType = journalType,
                JournalId = journal?.Id,
                FiscalPeriodId = period.Period?.Id,
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

        /// <summary>
        /// Sources de TVA (taux, montant) d'un document : ses lignes, plus la TVA des frais de
        /// port en-tête éventuels (RG-FA1), ventilée sur son propre taux.
        /// </summary>
        private static IEnumerable<(decimal Rate, decimal Vat)> DocumentVatSources<TLine>(
            IEnumerable<TLine>? lines,
            Func<TLine, decimal> rateOf,
            Func<TLine, decimal> vatOf,
            decimal shippingAmountHt,
            decimal shippingVatRate)
        {
            if (lines != null)
            {
                foreach (var line in lines)
                    yield return (rateOf(line), vatOf(line));
            }

            if (shippingAmountHt > 0)
                yield return (shippingVatRate, shippingAmountHt * (shippingVatRate / 100m));
        }

        /// <summary>
        /// Construit les lignes de TVA ventilées par taux (Phase 2) : une ligne par taux, au compte
        /// résolu via le mapping CompanyVatRateAccount (ou le compte par défaut des paramètres).
        /// La somme des TVA ventilées égale exactement totalVat : l'écart d'arrondi (4 décimales)
        /// est reporté sur le groupe le plus important. Sans source exploitable, une seule ligne au
        /// compte par défaut (comportement historique). L'équilibre débit = crédit est préservé.
        /// </summary>
        private static async Task<List<AccountingEntryLine>> BuildVatLinesAsync(
            IStorageBroker storage,
            string? companyId,
            IEnumerable<(decimal Rate, decimal Vat)> sources,
            decimal totalVat,
            bool isCollected,
            string label,
            bool debitSide)
        {
            // Regroupe la TVA par taux.
            var groups = new List<(decimal Rate, decimal Vat)>();
            foreach (var (rate, vat) in sources)
            {
                if (vat == 0) continue;
                var index = groups.FindIndex(g => g.Rate == rate);
                if (index >= 0) groups[index] = (rate, groups[index].Vat + vat);
                else groups.Add((rate, vat));
            }

            var lines = new List<AccountingEntryLine>();
            if (groups.Count == 0)
            {
                // Fallback : une seule ligne au compte par défaut des paramètres.
                var fallbackAccount = await AccountingEntryResolver.ResolveVatAccountsAsync(storage, companyId, 0m, isCollected);
                lines.Add(Line(fallbackAccount, label, debitSide ? totalVat : 0, debitSide ? 0 : totalVat));
                return lines;
            }

            // Arrondi 4 décimales par groupe, puis report de l'écart sur le groupe le plus important.
            var rounded = groups.Select(g => (g.Rate, Vat: Math.Round(g.Vat, 4))).ToList();
            var delta = Math.Round(totalVat, 4) - rounded.Sum(g => g.Vat);
            if (delta != 0)
            {
                var biggestIndex = 0;
                for (var i = 1; i < rounded.Count; i++)
                {
                    if (Math.Abs(rounded[i].Vat) > Math.Abs(rounded[biggestIndex].Vat)) biggestIndex = i;
                }
                rounded[biggestIndex] = (rounded[biggestIndex].Rate, rounded[biggestIndex].Vat + delta);
            }

            foreach (var (rate, vat) in rounded)
            {
                var account = await AccountingEntryResolver.ResolveVatAccountsAsync(storage, companyId, rate, isCollected);
                lines.Add(Line(account, label, debitSide ? vat : 0, debitSide ? 0 : vat));
            }
            return lines;
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
