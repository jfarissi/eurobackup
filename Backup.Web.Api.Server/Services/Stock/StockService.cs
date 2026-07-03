using System;
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

	public async Task<bool> UpdateFromDeliveryIfMatchAsync(int invoiceId, int deliveryId, CancellationToken ct = default, bool forceUpdate = false)
	{
		// 0) Vérifier si ce BL a déjà été utilisé pour alimenter le stock
		var relation = await this.storage.SelectRelationByInvoiceAndDeliveryAsync(invoiceId, deliveryId);
		if (relation != null && relation.StockUpdatedAt.HasValue && !forceUpdate)
		{
			// Ce BL a déjà été utilisé pour alimenter le stock
			// Mais si forceUpdate=true, on permet la mise à jour (correction d'erreur)
			return false;
		}

		// Récupérer le fournisseur du BL une seule fois
		var delivery = await this.storage.SelectDocumentByIdAsync(deliveryId);
		var deliverySupplier = delivery?.Supplier;

		// 1) PRIORITÉ: Utiliser directement les lignes parsées du BL (sans comparaison)
		// Cela permet de mettre à jour le stock même s'il y a des différences avec la facture
		var deliveryLines = this.storage.SelectLinesByDocumentId(deliveryId).ToList();
		if (deliveryLines.Count > 0)
		{
			// Charger les ajustements validés pour ce BL
			var adjustments = this.storage.SelectAdjustmentsByDeliveryId(deliveryId)
				.Where(a => a.IsValidated && a.ActualQuantity.HasValue)
				.ToList();
			
			System.Diagnostics.Debug.WriteLine($"[StockService] Found {adjustments.Count} validated adjustments for deliveryId={deliveryId}");
			foreach (var adj in adjustments)
			{
				System.Diagnostics.Debug.WriteLine($"[StockService] Adjustment: ProductKey={adj.ProductKey}, ActualQuantity={adj.ActualQuantity}");
			}
			
			// Créer un dictionnaire pour accéder rapidement aux ajustements par ProductKey
			var adjustmentMap = adjustments.ToDictionary(
				a => Backup.Web.Api.Server.Services.Documents.Parsing.ProductKeyHelper.Normalize(a.ProductKey), 
				a => a.ActualQuantity!.Value,
				StringComparer.OrdinalIgnoreCase
			);

			// Utiliser ProductKeyHelper pour normaliser les ProductKey

			// Si forceUpdate=true, charger les mises à jour précédentes pour calculer le delta correct
			Dictionary<string, decimal> previousQuantityMap = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
			if (forceUpdate && relation != null && relation.StockUpdatedAt.HasValue)
			{
				var previousUpdates = this.storage.SelectStockUpdatesByDeliveryId(deliveryId).ToList();
				foreach (var update in previousUpdates)
				{
					// La quantité précédente ajoutée est stockée dans QuantityDelta
					var normalizedKey = Backup.Web.Api.Server.Services.Documents.Parsing.ProductKeyHelper.Normalize(update.ProductKey);
					previousQuantityMap[normalizedKey] = update.QuantityDelta;
					System.Diagnostics.Debug.WriteLine($"[StockService] Previous update for {update.ProductKey}: QuantityDelta={update.QuantityDelta}");
				}
			}

			var changes = deliveryLines
				.Where(l => (l.Quantity != 0) && (!string.IsNullOrWhiteSpace(l.ProductCode) || !string.IsNullOrWhiteSpace(l.Product)))
				.Select(l => {
					var productKey = Backup.Web.Api.Server.Services.Documents.Parsing.ProductKeyHelper.GetProductKey(l);
					// Utiliser la quantité réelle validée si disponible, sinon la quantité du BL
					var quantityToUse = adjustmentMap.TryGetValue(productKey, out var actualQty) 
						? actualQty 
						: l.Quantity;
					
					// Si forceUpdate=true, calculer le delta par rapport à la quantité précédente
					decimal quantityDelta;
					if (forceUpdate && previousQuantityMap.TryGetValue(productKey, out var previousQty))
					{
						// Delta = nouvelle quantité - ancienne quantité
						quantityDelta = quantityToUse - previousQty;
						System.Diagnostics.Debug.WriteLine($"[StockService] ForceUpdate: ProductKey={productKey}, PreviousQty={previousQty}, NewQty={quantityToUse}, Delta={quantityDelta}");
					}
					else
					{
						// Première mise à jour : utiliser la quantité directement
						quantityDelta = quantityToUse;
					}
					
					System.Diagnostics.Debug.WriteLine($"[StockService] Product: {l.ProductCode ?? l.Product}, ProductKey={productKey}, DeliveryQty={l.Quantity}, UsingQty={quantityToUse}, QuantityDelta={quantityDelta}, HasAdjustment={adjustmentMap.ContainsKey(productKey)}");
					
					// Utiliser la même clé produit que celle utilisée pour les ajustements
					// pour assurer la cohérence avec le stock
					return (
						productKey: productKey, 
						quantityDelta: quantityDelta,
						supplier: deliverySupplier,
						description: l.Product,
						unit: l.Unit
					);
				})
				.ToList();
			if (changes.Count > 0)
			{
				await this.storage.UpsertStockBatchAsync(changes, deliveryId, invoiceId);
				
				// Marquer que ce BL a été utilisé pour alimenter le stock
				if (relation == null)
				{
					// Créer la relation si elle n'existe pas
					relation = new Backup.Web.Api.Server.Models.DocumentRelation
					{
						InvoiceId = invoiceId,
						DeliveryId = deliveryId,
						StockUpdatedAt = DateTime.UtcNow
					};
					await this.storage.InsertRelationAsync(relation);
				}
				else
				{
					// Mettre à jour la relation existante
					relation.StockUpdatedAt = DateTime.UtcNow;
					await this.storage.UpdateRelationAsync(relation);
				}
				return true;
			}
		}

		// 2) FALLBACK: Si pas de lignes parsées, utiliser la comparaison (mais seulement si pas de différences)
		// Cette méthode est moins fiable car elle nécessite une correspondance exacte
		var result = await this.comparer.CompareAsync(invoiceId, deliveryId, ct);
		if (result.Lines.Count == 0) return false;
		if (result.Lines.Any(l => l.Diff != 0)) return false; // differences -> do not update stock

		// Utiliser les résultats de comparaison comme fallback

		var fallbackChanges = result.Lines
			.Where(l => l.DeliveryQty != 0 && !string.IsNullOrWhiteSpace(l.Product))
			.Select(l => (
				productKey: l.Product.Trim(), 
				quantityDelta: l.DeliveryQty,
				supplier: deliverySupplier,
				description: l.Product,
				unit: (string?)null
			))
			.ToList();

		if (fallbackChanges.Count == 0) return false;
		await this.storage.UpsertStockBatchAsync(fallbackChanges, deliveryId, invoiceId);
		
		// Marquer que ce BL a été utilisé pour alimenter le stock
		if (relation == null)
		{
			relation = new Backup.Web.Api.Server.Models.DocumentRelation
			{
				InvoiceId = invoiceId,
				DeliveryId = deliveryId,
				StockUpdatedAt = DateTime.UtcNow
			};
			await this.storage.InsertRelationAsync(relation);
		}
		else
		{
			relation.StockUpdatedAt = DateTime.UtcNow;
			await this.storage.UpdateRelationAsync(relation);
		}
		return true;
	}

	public async Task<BatchStockUpdateResult> UpdateFromAllDeliveriesForInvoiceAsync(int invoiceId, CancellationToken ct = default, bool forceUpdate = false)
	{
		var deliveryIds = this.storage.SelectAllRelations()
			.Where(r => r.InvoiceId == invoiceId)
			.Select(r => r.DeliveryId)
			.Distinct()
			.ToList();

		var result = new BatchStockUpdateResult
		{
			InvoiceId = invoiceId,
			TotalDeliveries = deliveryIds.Count
		};

		foreach (var deliveryId in deliveryIds)
		{
			var updated = await this.UpdateFromDeliveryIfMatchAsync(invoiceId, deliveryId, ct, forceUpdate);
			if (updated)
			{
				result.UpdatedDeliveries++;
				result.UpdatedDeliveryIds.Add(deliveryId);
			}
			else
			{
				result.SkippedDeliveries++;
				result.SkippedDeliveryIds.Add(deliveryId);
			}
		}

		return result;
	}
	}
}


