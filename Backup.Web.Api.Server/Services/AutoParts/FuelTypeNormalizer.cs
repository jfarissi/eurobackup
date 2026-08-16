using System;
using System.Globalization;

namespace Backup.Web.Api.Server.Services.AutoParts
{
    /// <summary>Normalise TecDoc / NHTSA / RapidAPI vers Essence, Diesel, Hybride, Électrique, GPL.</summary>
    public static class FuelTypeNormalizer
    {
        public static string? Normalize(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var s = raw.Trim();
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                return FromTecDocId(id);

            var low = s.ToLowerInvariant();
            if (LooksDiesel(low)) return "Diesel";
            if (LooksPetrol(low)) return "Essence";
            if (low.Contains("hybrid") || low.Contains("hybride")) return "Hybride";
            if (low.Contains("electr") || low.Contains("électr")) return "Électrique";
            if (low.Contains("lpg") || low.Contains("gpl") || low.Contains("cng") || low.Contains("gnv"))
                return "GPL / GNV";
            return null;
        }

        /// <summary>Déduit Essence/Diesel depuis un libellé type TecDoc (ex. « 1.5 dCi 110 »).</summary>
        public static string? FromText(string? text) => Normalize(text);

        public static string? FromEngineCode(string? engineCode)
        {
            if (string.IsNullOrWhiteSpace(engineCode)) return null;
            return Normalize(engineCode.Trim());
        }

        public static string? FromTecDocId(int id) => id switch
        {
            1 => "Essence",
            2 => "Diesel",
            3 => "Éthanol",
            4 => "Électrique",
            5 => "Hybride",
            6 => "Hydrogène",
            7 => "GPL / GNV",
            8 => "GPL / GNV",
            9 => "GPL / GNV",
            10 => "Hybride",
            11 => "Hybride",
            _ => null
        };

        private static bool LooksDiesel(string low) =>
            low.Contains("diesel") || low.Contains("gazole") || low.Contains("tdi")
            || low.Contains("hdi") || low.Contains("dci") || low.Contains("cdti")
            || low.Contains("crdi") || low.Contains("jtd") || low.Contains("dti")
            || low.Contains("bluetec") || low.Contains("common rail");

        private static bool LooksPetrol(string low) =>
            low.Contains("essence") || low.Contains("gasoline") || low.Contains("petrol")
            || low.Contains("benzin") || low.Contains("otto") || low.Contains("sans plomb")
            || low.Contains("unleaded") || low.Contains("tce") || low.Contains("tsi")
            || low.Contains("tfsi") || low.Contains("thp") || low.Contains("mpi")
            || low.Contains("vti") || low.Contains("t-gdi") || low.Contains("flexfuel");
    }
}
