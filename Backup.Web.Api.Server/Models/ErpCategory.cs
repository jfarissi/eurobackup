using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Models
{
    /// <summary>
    /// Catégorie hiérarchique dérivée de MainType / Type / SubType ERP.
    /// Level: MainType = 1, Type = 2, SubType = 3.
    /// </summary>
    public class ErpCategory
    {
        public int Id { get; set; }

        /// <summary>ID ERP d'origine (MainTypeID / TypeID / SubTypeID).</summary>
        public string ErpExternalId { get; set; } = string.Empty;

        /// <summary>MainType | Type | SubType</summary>
        public string Level { get; set; } = string.Empty;

        public string NameNl { get; set; } = string.Empty;
        public string NameFr { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public string SlugNl { get; set; } = string.Empty;
        public string SlugFr { get; set; } = string.Empty;
        public string SlugEn { get; set; } = string.Empty;

        public int? ParentId { get; set; }
        public ErpCategory? Parent { get; set; }
        public ICollection<ErpCategory> Children { get; set; } = new List<ErpCategory>();

        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<ErpProduct> Products { get; set; } = new List<ErpProduct>();
    }
}
