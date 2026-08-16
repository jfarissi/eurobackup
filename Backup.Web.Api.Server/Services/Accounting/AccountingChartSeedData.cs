using System.Collections.Generic;

namespace Backup.Web.Api.Server.Services.Accounting
{
    /// <summary>Ligne de plan comptable à seeder (numéro, intitulé, classe, type, flags).</summary>
    public sealed record ChartOfAccountSeed(
        string AccountNumber,
        string Label,
        int AccountClass,
        string AccountType,
        bool IsLettrable,
        bool IsBilan,
        bool IsResultat);

    /// <summary>
    /// Plans comptables de base seedés par société.
    /// PCM Maroc : repris de docs/comptabilite.txt (~90 comptes, classes 1-7).
    /// PCG Europe : plan simplifié cohérent avec les comptes en dur d'AccountingLedger.
    /// </summary>
    public static class AccountingChartSeedData
    {
        public static IReadOnlyList<ChartOfAccountSeed> PcmMaroc { get; } = new[]
        {
            // CLASSE 1 : COMPTES DE RESSOURCES DURABLES
            A("111000", "Capital social", 1, "CapitauxPropres", false, true, false),
            A("112000", "Primes d'émission", 1, "CapitauxPropres", false, true, false),
            A("121000", "Résultats reportés", 1, "CapitauxPropres", false, true, false),
            A("129000", "Résultat de l'exercice (bénéfice)", 1, "CapitauxPropres", false, true, false),
            A("129100", "Résultat de l'exercice (perte)", 1, "CapitauxPropres", false, true, false),
            A("131000", "Subventions d'investissement", 1, "CapitauxPropres", false, true, false),
            A("148000", "Autres emprunts", 1, "Passif", false, true, false),
            A("161000", "Emprunts obligataires", 1, "Passif", false, true, false),
            A("162000", "Emprunts auprès des établissements de crédit", 1, "Passif", false, true, false),

            // CLASSE 2 : COMPTES D'ACTIF IMMOBILISE
            A("211000", "Frais d'établissement", 2, "Actif", false, true, false),
            A("212000", "Charges à répartir", 2, "Actif", false, true, false),
            A("221000", "Brevets, licences, logiciels", 2, "Actif", false, true, false),
            A("231100", "Terrains", 2, "Actif", false, true, false),
            A("233100", "Bâtiments", 2, "Actif", false, true, false),
            A("233200", "Installations techniques", 2, "Actif", false, true, false),
            A("234000", "Matériel de transport", 2, "Actif", false, true, false),
            A("235100", "Matériel de bureau", 2, "Actif", false, true, false),
            A("235200", "Matériel informatique", 2, "Actif", false, true, false),
            A("251000", "Titres de participation", 2, "Actif", false, true, false),
            A("281100", "Amortissements des frais d'établissement", 2, "Actif", false, true, false),
            A("283100", "Amortissements des bâtiments", 2, "Actif", false, true, false),
            A("283300", "Amortissements des installations techniques", 2, "Actif", false, true, false),
            A("283400", "Amortissements du matériel de transport", 2, "Actif", false, true, false),
            A("283500", "Amortissements du matériel de bureau", 2, "Actif", false, true, false),

            // CLASSE 3 : COMPTES DE STOCKS
            A("311000", "Marchandises", 3, "Actif", false, true, false),
            A("312100", "Matières premières", 3, "Actif", false, true, false),
            A("312300", "Matières consommables", 3, "Actif", false, true, false),
            A("321100", "Emballages commerciaux", 3, "Actif", false, true, false),
            A("341100", "Produits en cours", 3, "Actif", false, true, false),
            A("355000", "Produits finis", 3, "Actif", false, true, false),

            // CLASSE 4 : COMPTES DE TIERS
            A("342100", "Clients - Factures à établir", 4, "Actif", true, true, false),
            A("342400", "Clients - Effets à recevoir", 4, "Actif", true, true, false),
            A("342500", "Clients - Doutes ou litigieux", 4, "Actif", true, true, false),
            A("345500", "Etat - TVA récupérable sur immobilisations", 4, "Actif", false, true, false),
            A("345600", "Etat - TVA récupérable sur charges", 4, "Actif", false, true, false),
            A("345800", "Etat - TVA récupérable crédit de TVA", 4, "Actif", false, true, false),
            A("348100", "Créances sur cessions d'immobilisations", 4, "Actif", true, true, false),
            A("349100", "Charges constatées d'avance", 4, "Actif", false, true, false),
            A("391100", "Dépréciation des stocks de marchandises", 4, "Actif", false, true, false),
            A("394200", "Dépréciation des créances clients", 4, "Actif", false, true, false),
            A("401100", "Fournisseurs", 4, "Passif", true, true, false),
            A("408100", "Fournisseurs - Factures non parvenues", 4, "Passif", true, true, false),
            A("421100", "Personnel - Salaires à payer", 4, "Passif", true, true, false),
            A("431100", "CNSS - Cotisations à payer", 4, "Passif", false, true, false),
            A("432100", "AMO - Cotisations à payer", 4, "Passif", false, true, false),
            A("442100", "Etat - Impôt sur les sociétés à payer", 4, "Passif", false, true, false),
            A("442300", "Etat - IGR à payer", 4, "Passif", false, true, false),
            A("445500", "Etat - TVA facturée", 4, "Passif", false, true, false),
            A("445600", "Etat - TVA due (ou crédit de TVA)", 4, "Passif", false, true, false),
            A("448100", "Etat - Charges à payer", 4, "Passif", false, true, false),
            A("449100", "Produits constatés d'avance", 4, "Passif", false, true, false),

            // CLASSE 5 : COMPTES DE TRESORERIE
            A("514100", "Banques locales (MAD)", 5, "Actif", true, true, false),
            A("514200", "Banques étrangères (devises)", 5, "Actif", true, true, false),
            A("516100", "Caisses locales", 5, "Actif", true, true, false),
            A("552000", "Crédits d'escompte", 5, "Passif", false, true, false),

            // CLASSE 6 : COMPTES DE CHARGES
            A("611100", "Achats de marchandises", 6, "Charge", false, false, true),
            A("612100", "Achats de matières premières", 6, "Charge", false, false, true),
            A("612200", "Achats de matières consommables", 6, "Charge", false, false, true),
            A("613100", "Locations et charges locatives", 6, "Charge", false, false, true),
            A("613400", "Entretien, réparations et maintenance", 6, "Charge", false, false, true),
            A("614100", "Etudes, recherches et documentation", 6, "Charge", false, false, true),
            A("614300", "Déplacements, missions et réceptions", 6, "Charge", false, false, true),
            A("614400", "Publicité, publications et relations publiques", 6, "Charge", false, false, true),
            A("616100", "Impôts et taxes directs", 6, "Charge", false, false, true),
            A("617100", "Rémunérations du personnel", 6, "Charge", false, false, true),
            A("617400", "Charges sociales", 6, "Charge", false, false, true),
            A("618100", "Dotations aux amortissements d'exploitation", 6, "Charge", false, false, true),
            A("631100", "Charges d'intérêts", 6, "Charge", false, false, true),
            A("638000", "Autres charges financières", 6, "Charge", false, false, true),
            A("651200", "Valeurs comptables des immobilisations cédées", 6, "Charge", false, false, true),
            A("658000", "Autres charges non courantes", 6, "Charge", false, false, true),

            // CLASSE 7 : COMPTES DE PRODUITS
            A("711100", "Ventes de marchandises au Maroc", 7, "Produit", false, false, true),
            A("711200", "Ventes de marchandises à l'étranger", 7, "Produit", false, false, true),
            A("712100", "Ventes de produits finis au Maroc", 7, "Produit", false, false, true),
            A("712700", "Ventes de produits intermédiaires", 7, "Produit", false, false, true),
            A("713100", "Variation des stocks de produits en cours", 7, "Produit", false, false, true),
            A("713400", "Variation des stocks de produits finis", 7, "Produit", false, false, true),
            A("718100", "Reprises sur dotations d'exploitation", 7, "Produit", false, false, true),
            A("738000", "Autres produits financiers", 7, "Produit", false, false, true),
            A("751200", "Produits des cessions d'immobilisations", 7, "Produit", false, false, true),
            A("758000", "Autres produits non courants", 7, "Produit", false, false, true),
        };

        public static IReadOnlyList<ChartOfAccountSeed> PcgEurope { get; } = new[]
        {
            // CLASSE 1 : CAPITAUX & DETTES LONG TERME
            A("101000", "Capital social", 1, "CapitauxPropres", false, true, false),
            A("110000", "Report à nouveau", 1, "CapitauxPropres", false, true, false),
            A("120000", "Résultat de l'exercice (bénéfice)", 1, "CapitauxPropres", false, true, false),
            A("129000", "Résultat de l'exercice (perte)", 1, "CapitauxPropres", false, true, false),
            A("164000", "Emprunts auprès des établissements de crédit", 1, "Passif", false, true, false),

            // CLASSE 2 : IMMOBILISATIONS
            A("205000", "Concessions, brevets, licences, logiciels", 2, "Actif", false, true, false),
            A("213000", "Constructions", 2, "Actif", false, true, false),
            A("215400", "Matériel industriel", 2, "Actif", false, true, false),
            A("218200", "Matériel de transport", 2, "Actif", false, true, false),
            A("218300", "Matériel de bureau et informatique", 2, "Actif", false, true, false),
            A("281500", "Amortissements des immobilisations corporelles", 2, "Actif", false, true, false),

            // CLASSE 3 : STOCKS
            A("310000", "Marchandises", 3, "Actif", false, true, false),
            A("355000", "Produits finis", 3, "Actif", false, true, false),

            // CLASSE 4 : TIERS
            A("401000", "Fournisseurs", 4, "Passif", true, true, false),
            A("408100", "Fournisseurs - Factures non parvenues", 4, "Passif", true, true, false),
            A("411000", "Clients", 4, "Actif", true, true, false),
            A("416000", "Clients douteux", 4, "Actif", true, true, false),
            A("419000", "Avances et acomptes reçus sur commandes", 4, "Passif", true, true, false),
            A("421000", "Personnel - Rémunérations dues", 4, "Passif", true, true, false),
            A("431000", "Sécurité sociale", 4, "Passif", false, true, false),
            A("444000", "Etat - Impôt sur les sociétés", 4, "Passif", false, true, false),
            A("445500", "TVA à décaisser", 4, "Passif", false, true, false),
            A("445660", "TVA déductible", 4, "Actif", false, true, false),
            A("445710", "TVA collectée", 4, "Passif", false, true, false),
            A("486000", "Charges constatées d'avance", 4, "Actif", false, true, false),
            A("487000", "Produits constatés d'avance", 4, "Passif", false, true, false),

            // CLASSE 5 : TRESORERIE
            A("512000", "Banque", 5, "Actif", true, true, false),
            A("530000", "Caisse", 5, "Actif", true, true, false),
            A("580000", "Virements internes", 5, "Actif", false, true, false),

            // CLASSE 6 : CHARGES
            A("607000", "Achats de marchandises", 6, "Charge", false, false, true),
            A("613000", "Locations", 6, "Charge", false, false, true),
            A("615000", "Entretien et réparations", 6, "Charge", false, false, true),
            A("622000", "Rémunérations d'intermédiaires et honoraires", 6, "Charge", false, false, true),
            A("626000", "Frais postaux et télécommunications", 6, "Charge", false, false, true),
            A("641000", "Rémunérations du personnel", 6, "Charge", false, false, true),
            A("645000", "Charges de sécurité sociale", 6, "Charge", false, false, true),
            A("661000", "Charges d'intérêts", 6, "Charge", false, false, true),
            A("681000", "Dotations aux amortissements", 6, "Charge", false, false, true),

            // CLASSE 7 : PRODUITS
            A("701000", "Ventes de marchandises", 7, "Produit", false, false, true),
            A("706000", "Prestations de services", 7, "Produit", false, false, true),
            A("758000", "Produits divers de gestion", 7, "Produit", false, false, true),
            A("761000", "Produits financiers", 7, "Produit", false, false, true),
        };

        private static ChartOfAccountSeed A(
            string number, string label, int accountClass, string accountType,
            bool lettrable, bool bilan, bool resultat) =>
            new(number, label, accountClass, accountType, lettrable, bilan, resultat);
    }
}
