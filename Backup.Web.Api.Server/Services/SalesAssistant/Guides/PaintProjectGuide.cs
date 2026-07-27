using System.Collections.Generic;

namespace Backup.Web.Api.Server.Services.SalesAssistant.Guides
{
    public sealed class PaintProjectGuide : MarkerProjectGuide
    {
        public static readonly PaintProjectGuide Instance = new();

        public override string DomainId => "painting";
        public override string Title => "Parcours peinture";
        public override int BaseFamilyCount => 2;

        public override IReadOnlyList<ProjectGuideStep> Families { get; } =
        [
            new()
            {
                Id = "paint",
                Label = "Peinture",
                AisleHint = "Peintures intérieur / extérieur",
                CartMarkers =
                [
                    "peinture", "paint", "latexverf", "acrylverf", "acrylique", "muurverf",
                    "satin", "glycéro", "alkyd", "formule 12", "muur & plafond", "muur en plafond"
                ],
                LookMarkers = ["peinture", "paint", "muurverf", "latexverf", "acrylique"],
                TypeHints = ["peinture", "muurverf", "latexverf"]
            },
            new()
            {
                Id = "primer",
                Label = "Sous-couche / primaire",
                AisleHint = "Primaires & sous-couches",
                CartMarkers =
                [
                    "sous-couche", "sous couche", "grondverf", "voorstrijk", "undercoat", "primaire",
                    "primer muur", "primer plafond", "primer m&p", "isoprim", "iso-prim"
                ],
                LookMarkers = ["sous-couche", "primer", "grondverf", "voorstrijk", "isoprim"],
                TypeHints = ["sous-couche", "primer", "grondverf", "isoprim"]
            },
            new()
            {
                Id = "roller",
                Label = "Rouleau / pinceau",
                AisleHint = "Outillage peinture",
                CartMarkers =
                [
                    "rouleau", "roller", "verfroller", "pinceau", "kwast", "borstel", "paint roller", "manchon"
                ],
                LookMarkers = ["rouleau", "roller", "pinceau", "kwast"],
                TypeHints = ["rouleau", "pinceau", "verfroller"]
            },
            new()
            {
                Id = "tape",
                Label = "Adhésif de masquage",
                AisleHint = "Rubans & protection",
                CartMarkers =
                [
                    "schilderstape", "masking tape", "afplaktape", "malertape", "afplakband",
                    "ruban de masquage", "masking", "ruban masquage"
                ],
                LookMarkers = ["masking", "schilderstape", "afplaktape", "ruban"],
                TypeHints = ["schilderstape", "afplaktape", "masking tape"]
            }
        ];
    }
}
