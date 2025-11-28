using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Services.Documents;

namespace Backup.Web.Api.Server.Services.Stock
{
	public class StockService : IStockService
	{
		private readonly IStorageBroker storage;
		private readonly IDocumentComparisonService comparer;

		public StockService(IStorageBroker storage, IDocumentComparisonService comparer)
		{
			this.storage = storage;
			this.comparer = comparer;
		}

		public async Task<bool> UpdateFromDeliveryIfMatchAsync(int invoiceId, int deliveryId, CancellationToken ct = default)
		{
			// 1) Run comparison (current implementation based on ContentText)
			var result = await this.comparer.CompareAsync(invoiceId, deliveryId, ct);
			if (result.Lines.Count == 0) return false;
			if (result.Lines.Any(l => l.Diff != 0)) return false; // differences -> do not update stock

			// 2) Prefer using parsed DocumentLines to get ProductCode if available
			var deliveryLines = this.storage.SelectLinesByDocumentId(deliveryId).ToList();
			if (deliveryLines.Count > 0)
			{
				var changes = deliveryLines
					.Where(l => (l.Quantity != 0) && (!string.IsNullOrWhiteSpace(l.ProductCode) || !string.IsNullOrWhiteSpace(l.Product)))
					.Select(l => (productKey: (l.ProductCode ?? l.Product)!.Trim(), quantityDelta: l.Quantity))
					.ToList();
				if (changes.Count == 0) return false;
				await this.storage.UpsertStockBatchAsync(changes);
				return true;
			}

			// 3) Fallback: use comparison result product names as keys
			var fallbackChanges = result.Lines
				.Where(l => l.DeliveryQty != 0 && !string.IsNullOrWhiteSpace(l.Product))
				.Select(l => (productKey: l.Product.Trim(), quantityDelta: l.DeliveryQty))
				.ToList();

			if (fallbackChanges.Count == 0) return false;
			await this.storage.UpsertStockBatchAsync(fallbackChanges);
			return true;
		}
	}
}


