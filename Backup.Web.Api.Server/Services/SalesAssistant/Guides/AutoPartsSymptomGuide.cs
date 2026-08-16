using System.Collections.Generic;

namespace Backup.Web.Api.Server.Services.SalesAssistant.Guides
{
    /// <summary>F4 T1 : parcours Demo symptôme frein → pièces (DIAG-*).</summary>
    public sealed class AutoPartsSymptomGuide : MarkerProjectGuide
    {
        public static readonly AutoPartsSymptomGuide Instance = new();

        public override string DomainId => "auto_parts";
        public override string Title => "Diagnostic pièces auto";
        public override int BaseFamilyCount => 2;

        public override IReadOnlyList<ProjectGuideStep> Families { get; } =
        [
            new()
            {
                Id = "pads",
                Label = "Plaquettes",
                AisleHint = "Freinage — plaquettes",
                CartMarkers = ["plaquette", "plaquettes", "brake pad", "remblok", "diag-pad"],
                LookMarkers = ["plaquette", "usure", "squeak", "piepen", "bruit"],
                TypeHints = ["DIAG-PAD", "plaquette"]
            },
            new()
            {
                Id = "disc",
                Label = "Disque",
                AisleHint = "Freinage — disques",
                CartMarkers = ["disque de frein", "brake disc", "remschijf", "diag-disc"],
                LookMarkers = ["disque", "grince", "grinding", "vibration"],
                TypeHints = ["DIAG-DISC", "disque de frein"]
            },
            new()
            {
                Id = "caliper",
                Label = "Étrier",
                AisleHint = "Freinage — étriers",
                CartMarkers = ["étrier", "etrier", "caliper", "diag-cal"],
                LookMarkers = ["étrier", "etrier", "caliper", "fuite"],
                TypeHints = ["DIAG-CAL", "étrier"]
            },
            new()
            {
                Id = "kit",
                Label = "Kit frein",
                AisleHint = "Kits",
                CartMarkers = ["kit frein", "brake kit", "diag-kit"],
                LookMarkers = ["kit"],
                TypeHints = ["DIAG-KIT", "kit frein"]
            }
        ];
    }
}
