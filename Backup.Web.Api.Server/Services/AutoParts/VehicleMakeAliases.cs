using System;
using System.Collections.Generic;
using System.Linq;

namespace Backup.Web.Api.Server.Services.AutoParts
{
    /// <summary>
    /// Alias constructeurs véhicule (catalogue RapidAPI vs plaque/VIN/NHTSA).
    /// Ex. VW ↔ Volkswagen, Mercedes ↔ Mercedes-Benz.
    /// </summary>
    public static class VehicleMakeAliases
    {
        private static readonly string[][] Groups =
        {
            new[] { "vw", "volkswagen", "volkswagen ag" },
            new[] { "mercedes", "mercedes benz", "mercedes-benz", "mercedesbenz" },
            new[] { "citroen", "citroën" },
            new[] { "bmw", "bmw ag" },
            new[] { "peugeot", "peugeot citroen", "psa peugeot citroen" },
            new[] { "opel", "vauxhall" },
            new[] { "seat", "cupra" },
            new[] { "skoda", "škoda" },
            new[] { "toyota", "toyota motor" },
            new[] { "hyundai", "hyundai motor" },
            new[] { "kia", "kia motors" },
            new[] { "renault", "renault sas" },
            new[] { "dacia", "automobile dacia" },
            new[] { "ford", "ford motor" },
            new[] { "fiat", "fiat chrysler", "stellantis" },
        };

        /// <summary>Retourne le terme + alias en minuscules (liste pour EF Contains / IN).</summary>
        public static List<string> Expand(string? make)
        {
            if (string.IsNullOrWhiteSpace(make))
                return new List<string>();

            var key = Normalize(make);
            var set = new HashSet<string>(StringComparer.Ordinal);
            AddVariants(set, make);

            foreach (var group in Groups)
            {
                if (!group.Any(a =>
                        Normalize(a) == key
                        || key.StartsWith(Normalize(a) + " ", StringComparison.Ordinal)))
                    continue;

                foreach (var a in group)
                    AddVariants(set, a);
            }

            return set.ToList();
        }

        public static bool Matches(string? catalogMake, string? queryMake)
        {
            if (string.IsNullOrWhiteSpace(catalogMake) || string.IsNullOrWhiteSpace(queryMake))
                return false;
            var aliases = Expand(queryMake);
            var raw = catalogMake.Trim().ToLowerInvariant();
            return aliases.Contains(raw) || aliases.Contains(Normalize(catalogMake));
        }

        private static void AddVariants(HashSet<string> set, string value)
        {
            var raw = value.Trim().ToLowerInvariant();
            set.Add(raw);
            set.Add(Normalize(value));
        }

        public static string Normalize(string value)
        {
            var s = value.Trim().ToLowerInvariant()
                .Replace('–', '-')
                .Replace('—', '-');
            while (s.Contains("  ", StringComparison.Ordinal))
                s = s.Replace("  ", " ", StringComparison.Ordinal);
            return s.Replace("-", " ", StringComparison.Ordinal).Trim();
        }
    }
}
