using System;
using System.Collections.Generic;
using System.Linq;

namespace Backup.Web.Api.Server.Services.AutoParts
{
    /// <summary>F4 T1 : règles Demo symptôme → refs catalogue (pas de TecDoc).</summary>
    public static class AutoPartsSymptomMatcher
    {
        public const string DomainId = "auto_parts";

        public static readonly SymptomRule[] Rules =
        {
            new("wear", new[] { "usure", "usé", "usee", "usée", "worn", "slijtage", "versleten" },
                new[] { "DIAG-PAD" }, "Usure → plaquettes"),
            new("squeal", new[]
                {
                    "bruit de frein", "frein grince", "freins grincent", "grince", "grincement",
                    "squeak", "squeal", "piepen", "remmen piepen"
                },
                new[] { "DIAG-PAD", "DIAG-DISC" }, "Bruit au freinage → plaquettes / disque"),
            new("grind", new[]
                {
                    "grinding", "métal contre métal", "metal contre metal", "frottement métallique",
                    "vibration au freinage", "vibre au freinage"
                },
                new[] { "DIAG-DISC", "DIAG-PAD" }, "Grincement / vibration → disque"),
            new("leak", new[] { "fuite étrier", "fuite etrier", "caliper leak", "étrier fuit", "etrier fuit" },
                new[] { "DIAG-CAL" }, "Fuite → étrier"),
            new("kit", new[] { "kit frein", "brake kit", "kit de frein", "front brake" },
                new[] { "DIAG-KIT", "DIAG-DISC", "DIAG-PAD", "DIAG-CAL" }, "Kit frein avant"),
            new("pads", new[] { "plaquette", "plaquettes", "brake pad", "remblok" },
                new[] { "DIAG-PAD" }, "Plaquettes"),
            new("disc", new[] { "disque de frein", "brake disc", "remschijf" },
                new[] { "DIAG-DISC" }, "Disque de frein"),
            new("caliper", new[] { "étrier", "etrier", "caliper" },
                new[] { "DIAG-CAL" }, "Étrier"),
        };

        private static readonly string[] DomainKeys =
        {
            "bruit de frein", "frein grince", "freins grincent", "grince", "grincement",
            "plaquette", "plaquettes", "disque de frein", "étrier", "etrier",
            "brake pad", "brake disc", "squeak", "squeal", "grinding brake",
            "usure plaquette", "pédale de frein", "pedale de frein",
            "vibration au freinage", "kit frein", "brake kit",
            "remmen piepen", "remschijf", "remblok", "caliper",
            "bruit roulement", "roulement de roue"
        };

        public static bool LooksLike(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var lower = text.ToLowerInvariant();
            return DomainKeys.Any(k => lower.Contains(k, StringComparison.OrdinalIgnoreCase))
                   || Rules.Any(r => r.Triggers.Any(t => lower.Contains(t, StringComparison.OrdinalIgnoreCase)));
        }

        public static IReadOnlyList<SymptomMatch> Match(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<SymptomMatch>();
            var lower = text.ToLowerInvariant();
            var hits = new List<SymptomMatch>();
            foreach (var rule in Rules)
            {
                if (!rule.Triggers.Any(t => lower.Contains(t, StringComparison.OrdinalIgnoreCase)))
                    continue;
                hits.Add(new SymptomMatch(rule.Code, rule.Reason, rule.ProductRefs));
            }

            return hits;
        }

        public static IReadOnlyList<string> TypeHintsFor(string? text)
        {
            var refs = Match(text).SelectMany(m => m.ProductRefs).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (refs.Count == 0 && LooksLike(text))
                return new[] { "DIAG-KIT", "DIAG-PAD", "DIAG-DISC", "DIAG-CAL" };
            return refs;
        }

        public readonly record struct SymptomRule(string Code, string[] Triggers, string[] ProductRefs, string Reason);

        public sealed class SymptomMatch
        {
            public SymptomMatch(string code, string reason, IReadOnlyList<string> productRefs)
            {
                Code = code;
                Reason = reason;
                ProductRefs = productRefs;
            }

            public string Code { get; }
            public string Reason { get; }
            public IReadOnlyList<string> ProductRefs { get; }
        }
    }
}
