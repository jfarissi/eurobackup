using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Backup.Web.Api.Server.Services.ErpSync
{
    /// <summary>Traduit les slugs car-api (anglais) en libellés français lisibles.</summary>
    public static class CarApiPartNameTranslator
    {
        private static readonly Dictionary<string, string> PhraseFr = new(StringComparer.OrdinalIgnoreCase)
        {
            ["shock-absorber"] = "amortisseur",
            ["gas-strut"] = "vérin à gaz",
            ["brake-pad"] = "plaquette de frein",
            ["brake-disc"] = "disque de frein",
            ["brake-caliper"] = "étrier de frein",
            ["master-cylinder"] = "maître-cylindre",
            ["wheel-bearing"] = "roulement de roue",
            ["ball-joint"] = "rotule",
            ["tie-rod"] = "biellette de direction",
            ["control-arm"] = "bras de suspension",
            ["stabilizer-bar"] = "barre stabilisatrice",
            ["cv-joint"] = "joint homocinétique",
            ["drive-shaft"] = "arbre de transmission",
            ["oil-filter"] = "filtre à huile",
            ["air-filter"] = "filtre à air",
            ["fuel-filter"] = "filtre à carburant",
            ["cabin-filter"] = "filtre d'habitacle",
            ["spark-plug"] = "bougie d'allumage",
            ["glow-plug"] = "bougie de préchauffage",
            ["timing-belt"] = "courroie de distribution",
            ["serpentine-belt"] = "courroie d'accessoires",
            ["water-pump"] = "pompe à eau",
            ["oil-pump"] = "pompe à huile",
            ["fuel-pump"] = "pompe à carburant",
            ["power-steering"] = "direction assistée",
            ["steering-rack"] = "crémaillère de direction",
            ["clutch-kit"] = "kit d'embrayage",
            ["clutch-disc"] = "disque d'embrayage",
            ["pressure-plate"] = "mécanisme d'embrayage",
            ["release-bearing"] = "butée d'embrayage",
            ["exhaust-manifold"] = "collecteur d'échappement",
            ["lambda-sensor"] = "sonde lambda",
            ["oxygen-sensor"] = "sonde lambda",
            ["wiper-blade"] = "balai d'essuie-glace",
            ["head-gasket"] = "joint de culasse",
            ["valve-cover"] = "couvercle de soupapes",
            ["oil-pan"] = "carter d'huile",
            ["radiator-hose"] = "durite de radiateur",
            ["fuel-injector"] = "injecteur de carburant",
            ["ignition-coil"] = "bobine d'allumage",
            ["starter-motor"] = "démarreur",
            ["alternator"] = "alternateur",
            ["turbocharger"] = "turbo",
            ["intercooler"] = "échangeur air/air",
            ["catalytic-converter"] = "pot catalytique",
            ["muffler"] = "silencieux",
            ["bumper-absorber"] = "absorbeur de pare-chocs",
            ["door-handle"] = "poignée de porte",
            ["side-mirror"] = "rétroviseur extérieur",
            ["fog-lamp"] = "feu antibrouillard",
            ["tail-light"] = "feu arrière",
            ["headlight"] = "phare",
            ["window-regulator"] = "lève-vitre",
            ["door-lock"] = "serrure de porte",
            ["hood-latch"] = "verrou de capot",
            ["trunk-lid"] = "coffre",
            ["leaf-spring"] = "ressort à lames",
            ["coil-spring"] = "ressort hélicoïdal",
            ["sway-bar"] = "barre anti-roulis",
            ["parking-brake"] = "frein de stationnement",
            ["hand-brake"] = "frein à main",
            ["ac-compressor"] = "compresseur de climatisation",
            ["ac-condenser"] = "condenseur de climatisation",
            ["heater-core"] = "radiateur de chauffage",
            // Libellés TecDoc / RapidAPI (anglais) — phrases longues d'abord
            ["brake-pad-set-disc-brake"] = "jeu de plaquettes de frein à disque",
            ["tensioner-pulley-timing-belt"] = "galet tendeur de courroie de distribution",
            ["brake-pad-set"] = "jeu de plaquettes de frein",
            ["brake-shoe-set"] = "jeu de mâchoires de frein",
            ["timing-belt-kit"] = "kit de courroie de distribution",
            ["tensioner-pulley"] = "galet tendeur",
        };

        private static readonly Dictionary<string, string> WordFr = new(StringComparer.OrdinalIgnoreCase)
        {
            ["rear"] = "arrière",
            ["front"] = "avant",
            ["left"] = "gauche",
            ["right"] = "droite",
            ["upper"] = "supérieur",
            ["lower"] = "inférieur",
            ["inner"] = "intérieur",
            ["outer"] = "extérieur",
            ["shock"] = "amortisseur",
            ["absorber"] = "absorbeur",
            ["spring"] = "ressort",
            ["strut"] = "jambe",
            ["suspension"] = "suspension",
            ["brake"] = "frein",
            ["pad"] = "plaquette",
            ["disc"] = "disque",
            ["caliper"] = "étrier",
            ["rotor"] = "disque",
            ["engine"] = "moteur",
            ["turbo"] = "turbo",
            ["piston"] = "piston",
            ["cylinder"] = "cylindre",
            ["gasket"] = "joint",
            ["filter"] = "filtre",
            ["belt"] = "courroie",
            ["hose"] = "durite",
            ["pump"] = "pompe",
            ["radiator"] = "radiateur",
            ["thermostat"] = "thermostat",
            ["sensor"] = "capteur",
            ["relay"] = "relais",
            ["fuse"] = "fusible",
            ["switch"] = "interrupteur",
            ["bulb"] = "ampoule",
            ["lamp"] = "feu",
            ["battery"] = "batterie",
            ["alternator"] = "alternateur",
            ["starter"] = "démarreur",
            ["clutch"] = "embrayage",
            ["gearbox"] = "boîte de vitesses",
            ["transmission"] = "transmission",
            ["differential"] = "différentiel",
            ["axle"] = "essieu",
            ["bearing"] = "roulement",
            ["bushing"] = "silentbloc",
            ["seal"] = "joint spi",
            ["boot"] = "soufflet",
            ["bumper"] = "pare-chocs",
            ["fender"] = "aile",
            ["hood"] = "capot",
            ["door"] = "porte",
            ["mirror"] = "rétroviseur",
            ["grille"] = "calandre",
            ["spoiler"] = "aileron",
            ["exhaust"] = "échappement",
            ["muffler"] = "silencieux",
            ["catalyst"] = "catalyseur",
            ["manifold"] = "collecteur",
            ["steering"] = "direction",
            ["rack"] = "crémaillère",
            ["rod"] = "tige",
            ["arm"] = "bras",
            ["joint"] = "rotule",
            ["link"] = "biellette",
            ["bar"] = "barre",
            ["mount"] = "support",
            ["bracket"] = "support",
            ["cover"] = "couvercle",
            ["housing"] = "carter",
            ["valve"] = "soupape",
            ["injector"] = "injecteur",
            ["coil"] = "bobine",
            ["plug"] = "bougie",
            ["wiper"] = "essuie-glace",
            ["blade"] = "balai",
            ["compressor"] = "compresseur",
            ["condenser"] = "condenseur",
            ["evaporator"] = "évaporateur",
            ["heater"] = "chauffage",
            ["ventilation"] = "ventilation",
            ["actuator"] = "actionneur",
            ["motor"] = "moteur",
            ["fan"] = "ventilateur",
            ["pulley"] = "poulie",
            ["tensioner"] = "tendeur",
            ["idler"] = "galet",
            ["kit"] = "kit",
            ["set"] = "jeu",
            ["shoe"] = "mâchoire",
            ["timing"] = "distribution",
            ["assembly"] = "ensemble",
            ["module"] = "module",
            ["unit"] = "unité",
            ["automatic"] = "automatique",
            ["manual"] = "manuel",
            ["electric"] = "électrique",
            ["hydraulic"] = "hydraulique",
            ["pneumatic"] = "pneumatique",
            ["trunk"] = "coffre",
            ["tailgate"] = "hayon",
            ["bonnet"] = "capot",
            ["fog"] = "antibrouillard",
            ["head"] = "avant",
            ["tail"] = "arrière",
            ["side"] = "latéral",
            ["cab"] = "cabine",
            ["trailer"] = "remorque",
            ["hook"] = "crochet",
            ["washer"] = "lave",
            ["cleaner"] = "nettoyant",
            ["lubricant"] = "lubrifiant",
            ["antigel"] = "antigel",
            ["adsorber"] = "adsorbeur",
            ["adjuster"] = "réglage",
            ["selector"] = "sélecteur",
            ["glove"] = "boîte",
            ["box"] = "boîte",
            ["lid"] = "couvercle",
            ["gas"] = "gaz",
        };

        public static string TranslateSlug(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return string.Empty;

            var normalized = slug.Trim().ToLowerInvariant();
            var consumed = new bool[normalized.Length];
            var parts = new List<(int Index, string Text)>();

            foreach (var phrase in PhraseFr.Keys.OrderByDescending(k => k.Length))
            {
                var idx = 0;
                while ((idx = normalized.IndexOf(phrase, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    var startOk = idx == 0 || normalized[idx - 1] == '-';
                    var endIdx = idx + phrase.Length;
                    var endOk = endIdx >= normalized.Length || normalized[endIdx] == '-';
                    if (startOk && endOk && !IsRangeConsumed(consumed, idx, phrase.Length))
                    {
                        parts.Add((idx, PhraseFr[phrase]));
                        MarkConsumed(consumed, idx, phrase.Length);
                    }

                    idx++;
                }
            }

            var tokens = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < tokens.Length; i++)
            {
                var tokenStart = normalized.IndexOf(tokens[i], StringComparison.Ordinal);
                if (IsTokenConsumed(consumed, tokenStart, tokens[i].Length))
                    continue;

                if (WordFr.TryGetValue(tokens[i], out var fr))
                    parts.Add((tokenStart, fr));
                else if (tokens[i].Length > 2)
                    parts.Add((tokenStart, tokens[i]));
            }

            if (parts.Count == 0)
                return HumanizeSlug(slug);

            var text = string.Join(' ', parts.OrderBy(p => p.Index).Select(p => p.Text));
            return CapitalizeFirst(text);
        }

        /// <summary>Traduit un libellé produit anglais (TecDoc/RapidAPI) vers le français.</summary>
        public static string TranslateEnglishName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var slug = Regex.Replace(name.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-")
                .Trim('-');
            return TranslateSlug(slug);
        }

        private static bool IsRangeConsumed(bool[] consumed, int start, int length)
        {
            for (var i = start; i < start + length && i < consumed.Length; i++)
            {
                if (consumed[i])
                    return true;
            }

            return false;
        }

        private static bool IsTokenConsumed(bool[] consumed, int start, int length)
        {
            for (var i = start; i < start + length && i < consumed.Length; i++)
            {
                if (consumed[i])
                    return true;
            }

            return false;
        }

        private static void MarkConsumed(bool[] consumed, int start, int length)
        {
            for (var i = start; i < start + length && i < consumed.Length; i++)
                consumed[i] = true;
        }

        private static string HumanizeSlug(string slug) =>
            CapitalizeFirst(slug.Replace('-', ' '));

        private static string CapitalizeFirst(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            var lower = text.ToLowerInvariant();
            return char.ToUpper(lower[0], CultureInfo.GetCultureInfo("fr-FR")) + lower[1..];
        }
    }
}
