using System;
using System.Collections.Generic;
using System.Linq;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    public static class SalesMaterialLexicon
    {
        public static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "je", "tu", "il", "on", "nous", "vous", "les", "des", "une", "un", "de", "du", "la", "le",
            "et", "ou", "pour", "avec", "sans", "dans", "sur", "par", "pas", "plus", "très", "tres",
            "veux", "voudrais", "besoin", "cherche", "trouver", "acheter", "faire", "aide", "aider",
            "projet", "svp", "s'il", "sil", "vous", "plait", "ça", "ca", "est", "que", "qui", "quoi",
            "comme", "aussi", "donc", "alors", "mais", "mon", "ma", "mes", "ton", "votre", "notre",
            "mètre", "metre", "metres", "mètres", "cm", "mm", "haut", "haute", "hauteur", "longeur",
            "longueur", "large", "largeur", "d", "l", "à", "a", "au", "aux", "construire", "mur",
            "produit", "produits", "marque", "ont", "avoir", "avez", "suis", "rechercher",
            "souhaite", "souhaitez", "donne", "donner", "liste", "voir", "montre", "montrer",
            "est-ce", "quelque", "chose", "choses", "merci", "bonjour", "salut",
            "nouveau", "nouvelle", "nouveaux", "nouvelles", "new", "nieuw", "nieuwe", "démarré", "demarre",
            "démarrer", "demarrer", "commencer", "start"
        };

        public static readonly Dictionary<string, string[]> MaterialSynonyms = new(StringComparer.OrdinalIgnoreCase)
        {
            ["brique"] = new[]
            {
                "brique", "briques", "briquetage",
                "baksteen", "bakstenen", "snelbouwsteen", "snelbouwstenen", "metselsteen", "metselstenen",
                "brick", "bricks"
            },
            ["mortier"] = new[]
            {
                "mortier", "mortiers",
                "mortel", "metselmortel", "voegmortel", "lijmmortel",
                "mortar"
            },
            ["ciment"] = new[]
            {
                "ciment", "ciments", "ciment portland",
                "cement", "portlandcement"
            },
            ["parpaing"] = new[]
            {
                "parpaing", "parpaings", "agglo", "aggloméré", "agglomere", "hourdis",
                "betonblok", "betonblokken", "snelbouwblok", "snelbouwblokken", "cellenblok", "cellenbeton",
                "kalkzandsteen", "ytong",
                "concrete block", "cinder block", "breeze block"
            },
            ["pierre"] = new[]
            {
                "pierre", "pierres", "moellon", "moellons",
                "natuursteen", "steen",
                "stone", "masonry"
            },
            ["ferraillage"] = new[]
            {
                "ferraille", "ferraillage", "armature", "treillis",
                "wapening", "betonijzer", "draadmat",
                "rebar", "reinforcement", "mesh"
            },
            ["sable"] = new[]
            {
                "sable", "gravier",
                "zand", "grind",
                "sand", "gravel"
            },
            ["carrelage"] = new[]
            {
                "carrelage", "carreau", "carreaux", "faïence", "faience",
                "tegel", "tegels", "vloertegel", "wandtegel",
                "tile", "tiles", "tiling"
            },
            ["peinture"] = new[]
            {
                "peinture", "peintures", "lasurer", "enduit",
                "verf", "muurverf", "latex", "beits",
                "paint", "coating", "plaster"
            },
        };

        public static List<string> ExtractTypeHints(string text)
        {
            var lower = text.ToLowerInvariant();
            return MaterialSynonyms
                .Where(kv => kv.Value.Any(s => lower.Contains(s, StringComparison.OrdinalIgnoreCase))
                             || lower.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                .Select(kv => kv.Key)
                .ToList();
        }

        public static IEnumerable<string> ExpandTypeHintTerms(string hint)
        {
            yield return hint;
            if (MaterialSynonyms.TryGetValue(hint, out var synonyms))
            {
                foreach (var synonym in synonyms)
                    yield return synonym;
            }
        }
    }
}
