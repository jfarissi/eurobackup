using System;
using System.Collections.Generic;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Server.Services.SalesAssistant.Guides
{
    /// <summary>
    /// Jardin : cleaning / landscaping / maintenance partagent le même registre sous plusieurs DomainId.
    /// </summary>
    public sealed class GardenProjectGuide : MarkerProjectGuide
    {
        public static readonly GardenProjectGuide Instance = new();

        private readonly string _domainId;

        private GardenProjectGuide(string domainId) => _domainId = domainId;

        private GardenProjectGuide() : this("garden_landscaping") { }

        public static GardenProjectGuide ForDomain(string? domainId) =>
            string.Equals(domainId, "garden_cleaning", StringComparison.OrdinalIgnoreCase)
                ? Cleaning
                : string.Equals(domainId, "garden_maintenance", StringComparison.OrdinalIgnoreCase)
                    ? Maintenance
                    : Landscaping;

        public static readonly GardenProjectGuide Cleaning = new("garden_cleaning");
        public static readonly GardenProjectGuide Landscaping = new("garden_landscaping");
        public static readonly GardenProjectGuide Maintenance = new("garden_maintenance");

        public override string DomainId => _domainId;
        public override string Title => "Parcours jardin";
        public override int BaseFamilyCount => 2;

        public override IReadOnlyList<ProjectGuideStep> Families =>
            string.Equals(_domainId, "garden_cleaning", StringComparison.OrdinalIgnoreCase)
                ? CleaningFamilies
                : LandscapingFamilies;

        private static readonly IReadOnlyList<ProjectGuideStep> CleaningFamilies =
        [
            new()
            {
                Id = "tool",
                Label = "Outil / matériel de nettoyage",
                AisleHint = "Jardin · outils",
                CartMarkers =
                [
                    "souffleur", "blower", "bladblazer", "rateau", "hark", "balais", "bezem",
                    "tondeuse", "maaier", "déchets verts", "sac jardin", "tuinafval"
                ],
                LookMarkers = ["nettoyer", "souffleur", "rateau", "déchets", "tuin"],
                TypeHints = ["outil jardin", "souffleur", "rateau"]
            },
            new()
            {
                Id = "waste",
                Label = "Sacs / gestion déchets",
                AisleHint = "Sacs & évacuation",
                CartMarkers =
                [
                    "sac jardin", "tuinafvalzak", "afvalzak", "big bag", "déchets verts"
                ],
                LookMarkers = ["sac", "déchets", "afval"],
                TypeHints = ["sac jardin", "afvalzak"]
            },
            new()
            {
                Id = "gloves",
                Label = "Protection (gants)",
                AisleHint = "EPI jardin",
                CartMarkers = ["gant", "handschoen", "gloves"],
                LookMarkers = ["gant", "handschoen"],
                TypeHints = ["gants jardin"]
            }
        ];

        private static readonly IReadOnlyList<ProjectGuideStep> LandscapingFamilies =
        [
            new()
            {
                Id = "surface",
                Label = "Surface (dalles / gravier)",
                AisleHint = "Dalles, gravier, paillage",
                CartMarkers =
                [
                    "dalle", "tegel tuin", "gravel", "gravier", "split", "paillage", "mulch",
                    "bark", "schors", "gazon", "graszaad"
                ],
                LookMarkers = ["dalle", "gravier", "gravel", "paillage", "gazon"],
                TypeHints = ["dalle", "gravier"]
            },
            new()
            {
                Id = "border",
                Label = "Bordure",
                AisleHint = "Bordures & opsluitband",
                CartMarkers =
                [
                    "bordure", "border", "opsluitband", "kantopsluiting"
                ],
                LookMarkers = ["bordure", "opsluitband", "border"],
                TypeHints = ["bordure", "opsluitband"]
            },
            new()
            {
                Id = "geo",
                Label = "Géotextile",
                AisleHint = "Géotextile anti-mauvaises herbes",
                CartMarkers =
                [
                    "geotextile", "géotextile", "worteldoek", "anti-wortel", "antiwortel"
                ],
                LookMarkers = ["geotextile", "géotextile", "worteldoek"],
                TypeHints = ["geotextile", "worteldoek"]
            },
            new()
            {
                Id = "fill",
                Label = "Remplissage / finition",
                AisleHint = "Sable, ciment bordure, fixations",
                CartMarkers =
                [
                    "sable", "zand", "stabilisé", "stabilise", "ciment bordure"
                ],
                LookMarkers = ["sable", "zand", "remplissage"],
                TypeHints = ["sable", "zand"]
            }
        ];

        public override ProjectGuideStep ResolveNext(
            StoreChatSession session,
            string? userText,
            ProductSearchFilter? meta = null) =>
            base.ResolveNext(session, userText, meta);
    }
}
