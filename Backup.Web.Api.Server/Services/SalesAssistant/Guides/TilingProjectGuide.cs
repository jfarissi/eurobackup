using System.Collections.Generic;

namespace Backup.Web.Api.Server.Services.SalesAssistant.Guides
{
    public sealed class TilingProjectGuide : MarkerProjectGuide
    {
        public static readonly TilingProjectGuide Instance = new();

        public override string DomainId => "tiling";
        public override string Title => "Parcours carrelage";
        public override int BaseFamilyCount => 2;

        public override IReadOnlyList<ProjectGuideStep> Families { get; } =
        [
            new()
            {
                Id = "tiles",
                Label = "Carreaux",
                AisleHint = "Carrelage sol / mur",
                CartMarkers =
                [
                    "carreau", "carrelage", "tegel", "tegels", "tile", "faïence", "faience",
                    "gres", "grès", "keramisch"
                ],
                LookMarkers = ["carrelage", "carreau", "tegel", "tile"],
                TypeHints = ["carrelage", "tegel"]
            },
            new()
            {
                Id = "adhesive",
                Label = "Colle carrelage",
                AisleHint = "Colles & mortiers carrelage",
                CartMarkers =
                [
                    "colle carrelage", "tegellijm", "tile adhesive", "lijm voor tegels", "colle à carrelage"
                ],
                LookMarkers = ["colle", "lijm", "adhesive"],
                TypeHints = ["colle carrelage", "tegellijm"]
            },
            new()
            {
                Id = "grout",
                Label = "Joint",
                AisleHint = "Joints carrelage",
                CartMarkers =
                [
                    "joint carrelage", "voegmiddel", "grout", "voeg", "mortier joint"
                ],
                LookMarkers = ["joint", "voeg", "grout"],
                TypeHints = ["joint", "voegmiddel"]
            },
            new()
            {
                Id = "primer",
                Label = "Primaire",
                AisleHint = "Primaires support",
                CartMarkers =
                [
                    "primer", "primaire", "voorstrijk", "hechtprimer"
                ],
                LookMarkers = ["primaire", "primer", "voorstrijk"],
                TypeHints = ["primaire", "primer"]
            }
        ];
    }
}
