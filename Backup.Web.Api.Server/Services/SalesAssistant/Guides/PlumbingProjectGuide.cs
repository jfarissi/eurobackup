using System.Collections.Generic;

namespace Backup.Web.Api.Server.Services.SalesAssistant.Guides
{
    public sealed class PlumbingProjectGuide : MarkerProjectGuide
    {
        public static readonly PlumbingProjectGuide Instance = new();

        public override string DomainId => "plumbing";
        public override string Title => "Parcours plomberie";
        public override int BaseFamilyCount => 1;

        public override IReadOnlyList<ProjectGuideStep> Families { get; } =
        [
            new()
            {
                Id = "fixture",
                Label = "Appareil / produit principal",
                AisleHint = "Robinetterie, WC, évier…",
                CartMarkers =
                [
                    "robinet", "kraan", "mitigeur", "mengkraan", "wc", "toilet", "évier", "evier",
                    "lavabo", "wastafel", "siphon", "douche"
                ],
                LookMarkers = ["robinet", "wc", "évier", "plomberie", "leiding"],
                TypeHints = ["robinet", "kraan"]
            },
            new()
            {
                Id = "fittings",
                Label = "Raccords / joints",
                AisleHint = "Raccords & étanchéité",
                CartMarkers =
                [
                    "raccord", "fitting", "koppeling", "joint torique", "teflon", "ptfe",
                    "manchon", "coude", "knie"
                ],
                LookMarkers = ["raccord", "joint", "teflon", "fitting"],
                TypeHints = ["raccord", "koppeling"]
            },
            new()
            {
                Id = "pipe",
                Label = "Tuyau / flexible",
                AisleHint = "Tuyaux & flexibles",
                CartMarkers =
                [
                    "tuyau", "buis", "flexible", "slang", "pex", "multicouche", "cuivre", "koper"
                ],
                LookMarkers = ["tuyau", "flexible", "buis", "pex"],
                TypeHints = ["tuyau", "flexible"]
            }
        ];
    }
}
