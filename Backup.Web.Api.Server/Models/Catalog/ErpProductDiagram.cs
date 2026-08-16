using System;
using System.Collections.Generic;
using Backup.Web.Api.Server.Models;

namespace Backup.Web.Api.Server.Models.Catalog
{
    /// <summary>Schéma éclaté (F6) lié à un produit assemblage.</summary>
    public class ErpProductDiagram
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public int ProductId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        /// <summary>png | svg</summary>
        public string MediaKind { get; set; } = "png";
        /// <summary>demo | tecdoc | http</summary>
        public string Source { get; set; } = "demo";
        public int SortOrder { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ErpProduct? Product { get; set; }
        public List<ErpDiagramHotspot> Hotspots { get; set; } = new();
    }

    public class ErpDiagramHotspot
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid DiagramId { get; set; }
        public string Label { get; set; } = string.Empty;
        /// <summary>rect | circle | polygon</summary>
        public string Shape { get; set; } = "rect";
        /// <summary>Pourcentages image, ex. rect {"x":5,"y":15,"w":30,"h":50}.</summary>
        public string CoordsJson { get; set; } = "{}";
        public int TargetProductId { get; set; }
        public int SortOrder { get; set; }

        public ErpProductDiagram? Diagram { get; set; }
        public ErpProduct? TargetProduct { get; set; }
    }
}
