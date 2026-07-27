using System.Collections.Generic;

namespace Backup.Web.Api.Server.Services.SalesAssistant.Guides
{
    public sealed class RoofingProjectGuide : MarkerProjectGuide
    {
        public static readonly RoofingProjectGuide Instance = new();

        public override string DomainId => "roofing";
        public override string Title => "Parcours toiture";
        public override int BaseFamilyCount => 1;

        public override IReadOnlyList<ProjectGuideStep> Families { get; } =
        [
            new()
            {
                Id = "cover",
                Label = "Couverture / tuiles",
                AisleHint = "Tuiles, ardoises, plaques",
                CartMarkers =
                [
                    // Noms panier (CartOnlyHay = Name+Reference, sans catégorie) :
                    // Gevelpan / Vorstpan / Dakpaneel ne contiennent pas « dakpannen ».
                    "gevelpan", "vorstpan", "dakpaneel", "dakpannen", "tenord",
                    "waarborgpallet", "waarborgpalet", "tuile", "tuiles",
                    "ardoise", "leien", "plaque toiture", "dakplaat", "shingle",
                    "nokvorst", "edilians"
                ],
                LookMarkers = ["tuile", "tuiles", "dakpannen", "gevelpan", "dakpaneel", "ardoise", "couverture"],
                TypeHints = ["tuile", "dakpannen"]
            },
            new()
            {
                Id = "fixings",
                Label = "Crochets / fixations",
                AisleHint = "Fixations toiture",
                CartMarkers =
                [
                    "crochet", "panhaak", "panhaken", "dakhaak", "dakhaken",
                    "vis toiture", "dakschroef", "fixation toiture"
                ],
                LookMarkers = ["crochet", "panhaak", "fixation", "dakschroef"],
                TypeHints = ["crochet tuile", "panhaak"]
            },
            new()
            {
                Id = "gutter",
                Label = "Gouttière / évacuation",
                AisleHint = "Gouttières & descentes",
                CartMarkers =
                [
                    "gouttière", "gouttiere", "dakgoot", "descente", "afvoer", "regenpijp"
                ],
                LookMarkers = ["gouttière", "gouttiere", "dakgoot", "descente"],
                TypeHints = ["gouttière", "dakgoot"]
            }
        ];
    }
}
