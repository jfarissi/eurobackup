using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backup.Web.Api.Server.Services.Numbering
{
    public class NumberingSequenceService : INumberingSequenceService
    {
        private static readonly string[] DefaultDocumentTypes =
        {
            "Quote",
            "Order",
            "Invoice",
            "CreditNote",
            "PurchaseOrder",
            "SupplierInvoice",
            "DeliveryNote",
            "SalesDeliveryNote",
            "Receipt",
            "AccountingEntry",
            "SalesReturn",
            "SupplierCreditNote",
            "Proforma",
            "DepositInvoice",
            "SupplierRfq",
            "SupplierReturn",
            "Lettering"
        };

        private readonly IStorageBroker storageBroker;

        public NumberingSequenceService(IStorageBroker storageBroker)
        {
            this.storageBroker = storageBroker;
        }

        public async Task<string> GetNextNumberAsync(string documentType, string? companyId = null)
        {
            // RG-N2 : allocation atomique sous transaction (évite doublons / trous concurrentiels).
            if (this.storageBroker is StorageBroker db)
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                try
                {
                    var sequence = await db.DocumentNumberSequences
                        .FirstOrDefaultAsync(s => s.DocumentType == documentType && s.CompanyId == companyId);

                    if (sequence == null)
                    {
                        sequence = CreateDefaultSequence(documentType, companyId);
                        await db.DocumentNumberSequences.AddAsync(sequence);
                        await db.SaveChangesAsync();
                    }

                    var currentYear = DateTime.UtcNow.Year;
                    if (sequence.Year != currentYear)
                    {
                        sequence.Year = currentYear;
                        sequence.NextNumber = 1;
                    }

                    int number = sequence.NextNumber;
                    sequence.NextNumber++;
                    await db.SaveChangesAsync();
                    await tx.CommitAsync();
                    return FormatNumber(sequence, number);
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            }

            var fallback = await this.GetOrCreateSequenceAsync(documentType, companyId);
            var year = DateTime.UtcNow.Year;
            if (fallback.Year != year)
            {
                fallback.Year = year;
                fallback.NextNumber = 1;
            }

            int n = fallback.NextNumber;
            fallback.NextNumber++;
            await this.storageBroker.UpdateNumberSequenceAsync(fallback);
            return FormatNumber(fallback, n);
        }

        public async Task<string> PreviewNextNumberAsync(string documentType, string? companyId = null)
        {
            var sequence = await this.storageBroker.SelectNumberSequenceByTypeAsync(documentType, companyId);
            if (sequence == null)
            {
                sequence = CreateDefaultSequence(documentType, companyId);
            }

            var year = sequence.Year == DateTime.UtcNow.Year ? sequence.Year : DateTime.UtcNow.Year;
            var next = sequence.Year == DateTime.UtcNow.Year ? sequence.NextNumber : 1;
            var preview = new DocumentNumberSequence
            {
                Prefix = sequence.Prefix,
                Year = year,
                FormatPattern = sequence.FormatPattern
            };

            return FormatNumber(preview, next);
        }

        public async Task<IReadOnlyList<DocumentNumberSequence>> EnsureDefaultSequencesAsync(string? companyId = null)
        {
            var result = new List<DocumentNumberSequence>();
            foreach (var documentType in DefaultDocumentTypes)
            {
                var sequence = await this.GetOrCreateSequenceAsync(documentType, companyId);
                result.Add(sequence);
            }

            return result
                .OrderBy(s => Array.IndexOf(DefaultDocumentTypes, s.DocumentType))
                .ThenBy(s => s.DocumentType)
                .ToList();
        }

        private async Task<DocumentNumberSequence> GetOrCreateSequenceAsync(string documentType, string? companyId)
        {
            var sequence = await this.storageBroker.SelectNumberSequenceByTypeAsync(documentType, companyId);
            if (sequence != null)
            {
                return sequence;
            }

            sequence = CreateDefaultSequence(documentType, companyId);
            await this.storageBroker.InsertNumberSequenceAsync(sequence);
            return sequence;
        }

        private static DocumentNumberSequence CreateDefaultSequence(string documentType, string? companyId)
        {
            return new DocumentNumberSequence
            {
                DocumentType = documentType,
                Prefix = GetDefaultPrefix(documentType),
                Year = DateTime.UtcNow.Year,
                NextNumber = 1,
                FormatPattern = "{Prefix}{Year}-{Number:D4}",
                CompanyId = companyId
            };
        }

        private static string GetDefaultPrefix(string documentType) => documentType switch
        {
            "Invoice" => "FAC-",
            "SupplierInvoice" => "FAF-",
            "Quote" => "DEV-",
            "Order" => "CMD-",
            "DeliveryNote" => "BL-",
            "SalesDeliveryNote" => "BLV-",
            "CreditNote" => "AV-",
            "PurchaseOrder" => "CFA-",
            "Receipt" => "REC-",
            "AccountingEntry" => "EC-",
            "SalesReturn" => "BRC-",
            "SupplierCreditNote" => "AVF-",
            "Proforma" => "PRO-",
            "DepositInvoice" => "AAC-",
            "SupplierRfq" => "DPF-",
            "SupplierReturn" => "BRF-",
            "Lettering" => "LET-",
            _ => "DOC-"
        };

        private static string FormatNumber(DocumentNumberSequence sequence, int number)
        {
            return sequence.FormatPattern
                .Replace("{Prefix}", sequence.Prefix)
                .Replace("{Year}", sequence.Year.ToString())
                .Replace("{Number:D4}", number.ToString("D4"))
                .Replace("{Number:D5}", number.ToString("D5"))
                .Replace("{Number}", number.ToString());
        }
    }
}
