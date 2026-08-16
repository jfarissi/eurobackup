using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Services.SupplierQuotes
{
    public sealed class SupplierQuoteRequest
    {
        public int ProductId { get; init; }
        public string CompanyId { get; init; } = string.Empty;
        public string? Reference { get; init; }
        public string? Ean { get; init; }
        public string? Name { get; init; }
        public decimal? CatalogCost { get; init; }
        public decimal? CatalogStock { get; init; }
        public string? StockProductKey { get; init; }
    }

    public sealed class SupplierQuoteDto
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string FeedCode { get; set; } = string.Empty;
        public string? SupplierSku { get; set; }
        public decimal BuyPrice { get; set; }
        public decimal StockQty { get; set; }
        public int LeadDays { get; set; }
        public bool Available { get; set; }
        public string Source { get; set; } = "demo";
        public DateTime QuotedAt { get; set; }
        public bool IsBest { get; set; }
    }

    public sealed class SupplierQuotesResult
    {
        public int ProductId { get; set; }
        public int? BestSupplierId { get; set; }
        public string? ScoreReason { get; set; }
        public DateTime QuotedAt { get; set; }
        public IReadOnlyList<SupplierQuoteDto> Offers { get; set; } = Array.Empty<SupplierQuoteDto>();
    }
}
