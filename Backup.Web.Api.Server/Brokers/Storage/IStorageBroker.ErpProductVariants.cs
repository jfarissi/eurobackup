using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Catalog;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial interface IStorageBroker
    {
        IQueryable<ErpProductVariant> SelectAllErpProductVariants();
        ValueTask<ErpProductVariant?> SelectErpProductVariantByIdAsync(Guid id);
        ValueTask<ErpProductVariant> InsertErpProductVariantAsync(ErpProductVariant variant);
        ValueTask<ErpProductVariant> UpdateErpProductVariantAsync(ErpProductVariant variant);
        ValueTask<ErpProductVariant> DeleteErpProductVariantAsync(ErpProductVariant variant);

        IQueryable<ErpProductImage> SelectAllErpProductImages();
        ValueTask<ErpProductImage?> SelectErpProductImageByIdAsync(Guid id);
        ValueTask<ErpProductImage> InsertErpProductImageAsync(ErpProductImage image);
        ValueTask<ErpProductImage> UpdateErpProductImageAsync(ErpProductImage image);
        ValueTask<ErpProductImage> DeleteErpProductImageAsync(ErpProductImage image);

        IQueryable<ErpProductAttributeDefinition> SelectAllErpProductAttributeDefinitions();
        ValueTask<ErpProductAttributeDefinition?> SelectErpProductAttributeDefinitionByIdAsync(Guid id);
        ValueTask<ErpProductAttributeDefinition> InsertErpProductAttributeDefinitionAsync(ErpProductAttributeDefinition definition);
        ValueTask<ErpProductAttributeDefinition> UpdateErpProductAttributeDefinitionAsync(ErpProductAttributeDefinition definition);
        ValueTask<ErpProductAttributeDefinition> DeleteErpProductAttributeDefinitionAsync(ErpProductAttributeDefinition definition);

        IQueryable<ErpProductAttributeValue> SelectAllErpProductAttributeValues();
        ValueTask<ErpProductAttributeValue?> SelectErpProductAttributeValueByIdAsync(Guid id);
        ValueTask<ErpProductAttributeValue> InsertErpProductAttributeValueAsync(ErpProductAttributeValue value);
        ValueTask<ErpProductAttributeValue> UpdateErpProductAttributeValueAsync(ErpProductAttributeValue value);
        ValueTask<ErpProductAttributeValue> DeleteErpProductAttributeValueAsync(ErpProductAttributeValue value);

        IQueryable<ErpProductVehicle> SelectAllErpProductVehicles();
        ValueTask<ErpProductVehicle> InsertErpProductVehicleAsync(ErpProductVehicle vehicle);

        IQueryable<ErpOemCrossReference> SelectAllErpOemCrossReferences();
        ValueTask<ErpOemCrossReference> InsertErpOemCrossReferenceAsync(ErpOemCrossReference oem);
    }
}
