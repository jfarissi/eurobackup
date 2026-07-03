using Backup.Web.Api.Server.Brokers.Storage;
using Microsoft.AspNetCore.Mvc;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class StockController : RESTFulController
	{
		private readonly IStorageBroker storage;

		public StockController(IStorageBroker storage)
		{
			this.storage = storage;
		}

		[HttpGet]
		public IActionResult GetAll([FromQuery] string? search = null)
		{
			var query = this.storage.SelectAllStock();
			
			// Filtrer par recherche si fourni
			if (!string.IsNullOrWhiteSpace(search))
			{
				var searchLower = search.ToLowerInvariant();
				query = query.Where(s => 
					s.ProductKey.ToLower().Contains(searchLower)
				);
			}
			
			var items = query
				.OrderBy(s => s.ProductKey)
				.ToList();
			
			return Ok(items);
		}

		[HttpGet("{id:int}")]
		public IActionResult GetById([FromRoute] int id)
		{
			var item = this.storage.SelectAllStock()
				.FirstOrDefault(s => s.Id == id);
			
			if (item == null) return NotFound();
			return Ok(item);
		}

		[HttpGet("updates/by-delivery/{deliveryId:int}")]
		public IActionResult GetUpdatesByDelivery([FromRoute] int deliveryId)
		{
			var updates = this.storage.SelectStockUpdatesByDeliveryId(deliveryId).ToList();
			return Ok(updates);
		}

		[HttpGet("updates/by-product")]
		public IActionResult GetUpdatesByProduct([FromQuery] string productKey)
		{
			if (string.IsNullOrWhiteSpace(productKey)) return BadRequest("productKey required");
			var updates = this.storage.SelectStockUpdatesByProductKey(productKey).ToList();
			return Ok(updates);
		}
	}
}

