using System.Threading;
using System.Threading.Tasks;

namespace Backup.Web.Api.Server.Services.Stock
{
	public interface IStockService
	{
		/// <summary>
		/// If invoice and delivery match exactly (no diffs), increments stock by delivered quantities.
		/// Returns true if stock was updated; false if there were differences or an error.
		/// </summary>
		Task<bool> UpdateFromDeliveryIfMatchAsync(int invoiceId, int deliveryId, CancellationToken ct = default);
	}
}


