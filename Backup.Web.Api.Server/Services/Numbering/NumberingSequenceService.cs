using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;

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
            "Receipt"
        };

        private readonly IStorageBroker storageBroker;

        public NumberingSequenceService(IStorageBroker storageBroker)
        {
            this.storageBroker = storageBroker;
        }

        public async Task<string> GetNextNumberAsync(string documentType, string? companyId = null)
        {
            var sequence = await this.GetOrCreateSequenceAsync(documentType, companyId);
            var currentYear = DateTime.UtcNow.Year;

            if (sequence.Year != currentYear)
            {
                sequence.Year = currentYear;
                sequence.NextNumber = 1;
            }

            int number = sequence.NextNumber;
            sequence.NextNumber++;
            await this.storageBroker.UpdateNumberSequenceAsync(sequence);

            return FormatNumber(sequence, number);
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
