using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Backup.Web.Api.Server.Models;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial interface IStorageBroker
    {
        // Relations
        ValueTask<DocumentRelation> InsertRelationAsync(DocumentRelation relation);
        IQueryable<DocumentRelation> SelectAllRelations();
        ValueTask DeleteRelationAsync(int relationId);
        ValueTask<DocumentRelation?> SelectRelationByInvoiceAndDeliveryAsync(int invoiceId, int deliveryId);
        ValueTask UpdateRelationAsync(DocumentRelation relation);
        
        // Delivery Line Adjustments
        ValueTask<DeliveryLineAdjustment> InsertAdjustmentAsync(DeliveryLineAdjustment adjustment);
        ValueTask<DeliveryLineAdjustment> UpdateAdjustmentAsync(DeliveryLineAdjustment adjustment);
        IQueryable<DeliveryLineAdjustment> SelectAdjustmentsByDeliveryId(int deliveryId);
        ValueTask<DeliveryLineAdjustment?> SelectAdjustmentByDeliveryAndProductKeyAsync(int deliveryId, string productKey);
    }
}
