using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial class StorageBroker
    {
        public DbSet<ErpProductVariant> ErpProductVariants { get; set; } = null!;
        public DbSet<ErpProductImage> ErpProductImages { get; set; } = null!;
        public DbSet<ErpProductAttributeDefinition> ErpProductAttributeDefinitions { get; set; } = null!;
        public DbSet<ErpProductAttributeValue> ErpProductAttributeValues { get; set; } = null!;
        public DbSet<ErpProductVehicle> ErpProductVehicles { get; set; } = null!;
        public DbSet<ErpOemCrossReference> ErpOemCrossReferences { get; set; } = null!;

        public IQueryable<ErpProductVehicle> SelectAllErpProductVehicles() =>
            this.ErpProductVehicles.AsQueryable();

        public async ValueTask<ErpProductVehicle> InsertErpProductVehicleAsync(ErpProductVehicle vehicle)
        {
            if (vehicle.Id == Guid.Empty) vehicle.Id = Guid.NewGuid();
            vehicle.CreatedAt = DateTime.UtcNow;
            EntityEntry<ErpProductVehicle> entry = await this.ErpProductVehicles.AddAsync(vehicle);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<ErpOemCrossReference> SelectAllErpOemCrossReferences() =>
            this.ErpOemCrossReferences.AsQueryable();

        public async ValueTask<ErpOemCrossReference> InsertErpOemCrossReferenceAsync(ErpOemCrossReference oem)
        {
            if (oem.Id == Guid.Empty) oem.Id = Guid.NewGuid();
            oem.CreatedAt = DateTime.UtcNow;
            EntityEntry<ErpOemCrossReference> entry = await this.ErpOemCrossReferences.AddAsync(oem);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<ErpProductVariant> SelectAllErpProductVariants() =>
            this.ErpProductVariants.AsQueryable();

        public async ValueTask<ErpProductVariant?> SelectErpProductVariantByIdAsync(Guid id) =>
            await this.ErpProductVariants.FindAsync(id);

        public async ValueTask<ErpProductVariant> InsertErpProductVariantAsync(ErpProductVariant variant)
        {
            if (variant.Id == Guid.Empty) variant.Id = Guid.NewGuid();
            if (string.IsNullOrWhiteSpace(variant.Barcode)) variant.Barcode = null;
            if (string.IsNullOrWhiteSpace(variant.AttributesJson)) variant.AttributesJson = "{}";
            variant.CreatedAt = DateTime.UtcNow;
            EntityEntry<ErpProductVariant> entry = await this.ErpProductVariants.AddAsync(variant);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask<ErpProductVariant> UpdateErpProductVariantAsync(ErpProductVariant variant)
        {
            if (string.IsNullOrWhiteSpace(variant.Barcode)) variant.Barcode = null;
            variant.UpdatedAt = DateTime.UtcNow;
            this.ErpProductVariants.Update(variant);
            await this.SaveChangesAsync();
            return variant;
        }

        public async ValueTask<ErpProductVariant> DeleteErpProductVariantAsync(ErpProductVariant variant)
        {
            this.ErpProductVariants.Remove(variant);
            await this.SaveChangesAsync();
            return variant;
        }

        public IQueryable<ErpProductImage> SelectAllErpProductImages() =>
            this.ErpProductImages.AsQueryable();

        public async ValueTask<ErpProductImage?> SelectErpProductImageByIdAsync(Guid id) =>
            await this.ErpProductImages.FindAsync(id);

        public async ValueTask<ErpProductImage> InsertErpProductImageAsync(ErpProductImage image)
        {
            if (image.Id == Guid.Empty) image.Id = Guid.NewGuid();
            image.CreatedAt = DateTime.UtcNow;
            EntityEntry<ErpProductImage> entry = await this.ErpProductImages.AddAsync(image);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask<ErpProductImage> UpdateErpProductImageAsync(ErpProductImage image)
        {
            image.UpdatedAt = DateTime.UtcNow;
            this.ErpProductImages.Update(image);
            await this.SaveChangesAsync();
            return image;
        }

        public async ValueTask<ErpProductImage> DeleteErpProductImageAsync(ErpProductImage image)
        {
            this.ErpProductImages.Remove(image);
            await this.SaveChangesAsync();
            return image;
        }

        public IQueryable<ErpProductAttributeDefinition> SelectAllErpProductAttributeDefinitions() =>
            this.ErpProductAttributeDefinitions.AsQueryable();

        public async ValueTask<ErpProductAttributeDefinition?> SelectErpProductAttributeDefinitionByIdAsync(Guid id) =>
            await this.ErpProductAttributeDefinitions.FindAsync(id);

        public async ValueTask<ErpProductAttributeDefinition> InsertErpProductAttributeDefinitionAsync(ErpProductAttributeDefinition definition)
        {
            if (definition.Id == Guid.Empty) definition.Id = Guid.NewGuid();
            definition.CreatedAt = DateTime.UtcNow;
            EntityEntry<ErpProductAttributeDefinition> entry = await this.ErpProductAttributeDefinitions.AddAsync(definition);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask<ErpProductAttributeDefinition> UpdateErpProductAttributeDefinitionAsync(ErpProductAttributeDefinition definition)
        {
            definition.UpdatedAt = DateTime.UtcNow;
            this.ErpProductAttributeDefinitions.Update(definition);
            await this.SaveChangesAsync();
            return definition;
        }

        public async ValueTask<ErpProductAttributeDefinition> DeleteErpProductAttributeDefinitionAsync(ErpProductAttributeDefinition definition)
        {
            this.ErpProductAttributeDefinitions.Remove(definition);
            await this.SaveChangesAsync();
            return definition;
        }

        public IQueryable<ErpProductAttributeValue> SelectAllErpProductAttributeValues() =>
            this.ErpProductAttributeValues.AsQueryable();

        public async ValueTask<ErpProductAttributeValue?> SelectErpProductAttributeValueByIdAsync(Guid id) =>
            await this.ErpProductAttributeValues.FindAsync(id);

        public async ValueTask<ErpProductAttributeValue> InsertErpProductAttributeValueAsync(ErpProductAttributeValue value)
        {
            if (value.Id == Guid.Empty) value.Id = Guid.NewGuid();
            value.CreatedAt = DateTime.UtcNow;
            EntityEntry<ErpProductAttributeValue> entry = await this.ErpProductAttributeValues.AddAsync(value);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask<ErpProductAttributeValue> UpdateErpProductAttributeValueAsync(ErpProductAttributeValue value)
        {
            value.UpdatedAt = DateTime.UtcNow;
            this.ErpProductAttributeValues.Update(value);
            await this.SaveChangesAsync();
            return value;
        }

        public async ValueTask<ErpProductAttributeValue> DeleteErpProductAttributeValueAsync(ErpProductAttributeValue value)
        {
            this.ErpProductAttributeValues.Remove(value);
            await this.SaveChangesAsync();
            return value;
        }
    }
}
