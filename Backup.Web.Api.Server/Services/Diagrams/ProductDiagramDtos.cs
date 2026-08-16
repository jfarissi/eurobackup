using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Backup.Web.Api.Server.Services.Diagrams
{
    public sealed class ProductDiagramDto
    {
        public Guid Id { get; set; }
        public int ProductId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string MediaKind { get; set; } = "png";
        public string Source { get; set; } = "demo";
        public List<DiagramHotspotDto> Hotspots { get; set; } = new();
    }

    public sealed class DiagramHotspotDto
    {
        public Guid Id { get; set; }
        public string Label { get; set; } = string.Empty;
        public string Shape { get; set; } = "rect";
        public double X { get; set; }
        public double Y { get; set; }
        public double W { get; set; }
        public double H { get; set; }
        public int TargetProductId { get; set; }
        public string? TargetName { get; set; }
        public string? TargetReference { get; set; }

        public static DiagramHotspotDto FromJson(
            Guid id,
            string label,
            string shape,
            string coordsJson,
            int targetProductId,
            string? targetName,
            string? targetReference)
        {
            var dto = new DiagramHotspotDto
            {
                Id = id,
                Label = label,
                Shape = shape,
                TargetProductId = targetProductId,
                TargetName = targetName,
                TargetReference = targetReference,
                W = 20,
                H = 20
            };
            try
            {
                var coords = JsonSerializer.Deserialize<RectCoords>(coordsJson);
                if (coords != null)
                {
                    dto.X = coords.X;
                    dto.Y = coords.Y;
                    dto.W = coords.W;
                    dto.H = coords.H;
                }
            }
            catch (JsonException)
            {
                /* keep defaults */
            }
            return dto;
        }

        private sealed class RectCoords
        {
            [JsonPropertyName("x")] public double X { get; set; }
            [JsonPropertyName("y")] public double Y { get; set; }
            [JsonPropertyName("w")] public double W { get; set; }
            [JsonPropertyName("h")] public double H { get; set; }
        }
    }
}
