using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Backup.Web.Api.Server.Services.Stock
{
	public class BatchStockUpdateResult
	{
		public int InvoiceId { get; set; }
		public int TotalDeliveries { get; set; }
		public int UpdatedDeliveries { get; set; }
		public int SkippedDeliveries { get; set; }
		public List<int> UpdatedDeliveryIds { get; set; } = new();
		public List<int> SkippedDeliveryIds { get; set; } = new();
	}

	public interface IStockService
	{
		/// <summary>
		/// If invoice and delivery match exactly (no diffs), increments stock by delivered quantities.
		/// Returns true if stock was updated; false if there were differences or an error.
		/// </summary>
		Task<bool> UpdateFromDeliveryIfMatchAsync(int invoiceId, int deliveryId, CancellationToken ct = default, bool forceUpdate = false);
		Task<BatchStockUpdateResult> UpdateFromAllDeliveriesForInvoiceAsync(int invoiceId, CancellationToken ct = default, bool forceUpdate = false);
	}
}


