using System.Collections.Generic;

namespace Backup.Web.Api.Server.Services.SalesAssistant.Guides
{
    public sealed class ElectricalProjectGuide : MarkerProjectGuide
    {
        public static readonly ElectricalProjectGuide Instance = new();

        public override string DomainId => "electrical";
        public override string Title => "Parcours électricité";
        public override int BaseFamilyCount => 1;

        public override IReadOnlyList<ProjectGuideStep> Families { get; } =
        [
            new()
            {
                Id = "device",
                Label = "Appareillage / produit principal",
                AisleHint = "Électricité · prises, interrupteurs, luminaires",
                CartMarkers =
                [
                    "prise", "stopcontact", "interrupteur", "schakelaar", "luminaire", "lamp",
                    "ampoule", "e27", "e14", "spot", "led", "tableau", "zekering", "disjoncteur"
                ],
                LookMarkers = ["prise", "interrupteur", "lampe", "ampoule", "éclairage", "elektr", "e27"],
                TypeHints = ["ampoule", "lampe", "prise", "interrupteur"]
            },
            new()
            {
                Id = "fixings",
                Label = "Boîtes / fixations",
                AisleHint = "Boîtes d'encastrement, colliers",
                CartMarkers =
                [
                    "boîte", "boite", "inbouwdoos", "lasdoos", "collier", "kabelklem"
                ],
                LookMarkers = ["boîte", "boite", "inbouwdoos", "fixation"],
                TypeHints = ["boîte encastrement", "inbouwdoos"]
            },
            new()
            {
                Id = "cable",
                Label = "Câble / gaine",
                AisleHint = "Câbles & gaines",
                CartMarkers =
                [
                    "câble", "cable", "kabel", "gaine", "buis", "vdra", "xvb"
                ],
                LookMarkers = ["câble", "cable", "kabel", "gaine"],
                TypeHints = ["câble", "kabel"]
            }
        ];
    }
}
