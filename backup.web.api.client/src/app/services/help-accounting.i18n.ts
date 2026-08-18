/** Aide contextuelle des écrans Comptabilité (utilisateurs finaux, N0–N4). */
export type HelpDict = Record<string, string>;

export const HELP_ACCOUNTING_FR: HelpDict = {
  'help.accounting.tabs.title': 'Journal des écritures',
  'help.accounting.tabs.n1': 'Toutes les écritures (ventes, achats, paiements, saisie manuelle).',
  'help.accounting.tabs.body':
    'Cet écran liste les écritures générées automatiquement (factures, avoirs, paiements, achats) et les saisies manuelles.\nFiltrez par journal, type de pièce et période.\nLes autres menus Comptabilité (plan, journaux, exercices, lettrage, rapports, TVA, clôture, banque, exports, immos, paie, OCR, cabinet) se trouvent dans le menu de gauche.',
  'help.accounting.tabs.rules':
    'RG-EX3|Pas d’écriture hors exercice ouvert.\nRG-EX5|Exercice clôturé = consultation seule.\nRG-PM2|Période fermée = saisie bloquée.\nRG-EC1|Écriture manuelle : totaux débit = crédit.\nRG-SP1|Séparation des fonctions pour validations sensibles.',
  'help.accounting.tabs.example': 'Ex: Facture FAC-2026-0012 → écriture VEN 411 (clients) / 701 (ventes) / 44571 (TVA collectée).',
  'help.accounting.tabs.guide':
    'Filtrer la période et le journal\nOuvrir une écriture pour voir les lignes\nCréer une écriture manuelle si besoin\nÉquilibrer débit / crédit\nEnregistrer',
  'help.accounting.tabs.version': 'v1.1.0',

  'help.accounting.newEntry.title': 'Écriture comptable manuelle',
  'help.accounting.newEntry.n1': 'Saisie libre : journal, date, libellé et lignes débit/crédit.',
  'help.accounting.newEntry.body':
    'Utilisez ce formulaire pour une opération qui n’est pas générée par une facture (OD, régularisation, virement interne).\nChoisissez le journal, la date (dans une période ouverte) et au moins deux lignes.\nLes totaux débit et crédit doivent être égaux avant enregistrement.',
  'help.accounting.newEntry.rules':
    'RG-EC1|Σ débit = Σ crédit.\nRG-EX3|Date dans l’exercice / période ouverte.\nRG-EC2|Compte existant et actif dans le plan.',
  'help.accounting.newEntry.example': 'Ex: OD — 658 charges diverses 1 200 MAD (D) / 512 banque 1 200 MAD (C).',
  'help.accounting.newEntry.guide':
    'Choisir le journal et la date\nSaisir le libellé\nAjouter au moins 2 lignes (D et C)\nVérifier l’équilibre\nEnregistrer',
  'help.accounting.newEntry.version': 'v1.1.0',

  'help.accounting.chart.title': 'Plan de comptes',
  'help.accounting.chart.n1': 'Référentiel des comptes (PCG / PCM) : classes, types, lettrable.',
  'help.accounting.chart.body':
    'Le plan de comptes est le dictionnaire de la comptabilité : chaque ligne d’écriture pointe vers un numéro de compte.\nLe plan par défaut suit le PCG européen (classes 1 à 7). Vous pouvez ajouter des comptes, les marquer lettrables (411/401) ou les désactiver.\nNe supprimez pas un compte déjà mouvementé : désactivez-le.',
  'help.accounting.chart.rules':
    'RG-PC1|Numéro unique dans la société.\nRG-PC2|Classe 1–7 cohérente avec le premier chiffre.\nRG-PC3|Compte mouvementé : désactivation, pas suppression.\nRG-LT1|Lettrable = comptes de tiers (typiquement 411 / 401).',
  'help.accounting.chart.example': 'Ex: 411100 Clients — classe 4, type Asset, lettrable ; 701100 Ventes — classe 7, type Income.',
  'help.accounting.chart.guide':
    'Filtrer par classe ou rechercher un n°\nCréer un compte (n°, libellé, classe, type)\nCocher Lettrable pour les tiers\nEnregistrer\nUtiliser le n° dans les écritures',
  'help.accounting.chart.version': 'v1.0.0',

  'help.accounting.journals.title': 'Journaux comptables',
  'help.accounting.journals.n1': 'Codes ACH, VEN, BAN, CAIS, OD, AN — ventilation des pièces.',
  'help.accounting.journals.body':
    'Un journal regroupe les écritures d’un même circuit : achats (ACH), ventes (VEN), banque (BAN), caisse (CAIS), opérations diverses (OD), à-nouveaux (AN).\nLe compte de contrepartie (ex. 512 banque) est proposé automatiquement sur les journaux de trésorerie.',
  'help.accounting.journals.rules':
    'RG-JN1|Code journal unique (ACH, VEN, BAN, CAIS, OD, AN).\nRG-JN2|Contrepartie utile pour BAN / CAIS (compte 5).\nRG-JN3|Les écritures auto utilisent le journal du document (VEN, ACH…).',
  'help.accounting.journals.example': 'Ex: Journal BAN « Banque BMCE » — contrepartie 512100.',
  'help.accounting.journals.guide':
    'Ouvrir Journaux\nCréer un code (ACH, VEN, BAN…)\nSaisir le libellé\nIndiquer la contrepartie si banque/caisse\nEnregistrer',
  'help.accounting.journals.version': 'v1.0.0',

  'help.accounting.fiscalYears.title': 'Exercices et périodes',
  'help.accounting.fiscalYears.n1': 'Ouvrir un exercice (12 périodes) et verrouiller un mois.',
  'help.accounting.fiscalYears.body':
    'Un exercice = une année fiscale (souvent 1er janv. → 31 déc.). À l’ouverture, 12 périodes mensuelles sont créées.\nVerrouillez un mois clôturé pour empêcher toute nouvelle saisie. Un exercice clôturé n’accepte plus d’écritures (consultation seule).',
  'help.accounting.fiscalYears.rules':
    'RG-EX1|Un seul exercice ouvert à la fois (sauf ouverture N+1 en parallèle de clôture).\nRG-EX3|Saisie uniquement dans une période ouverte.\nRG-PM2|Période verrouillée = pas de nouvelle écriture.\nRG-EX5|Exercice clôturé = lecture seule.',
  'help.accounting.fiscalYears.example': 'Ex: Exercice 2026 ouvert, janvier et février verrouillés après déclaration TVA.',
  'help.accounting.fiscalYears.guide':
    'Ouvrir un exercice (dates de début / fin)\nContrôler les 12 périodes\nVerrouiller le mois une fois les contrôles faits\nDéverrouiller seulement pour une correction justifiée',
  'help.accounting.fiscalYears.version': 'v1.0.0',

  'help.accounting.lettrage.title': 'Lettrage (rapprochement tiers)',
  'help.accounting.lettrage.n1': 'Apparier factures et règlements sur 411 / 401.',
  'help.accounting.lettrage.body':
    'Le lettrage relie les lignes d’un même compte de tiers (clients 411, fournisseurs 401) jusqu’à solde zéro : facture + paiement, facture + avoir.\nLe lettrage automatique fait 3 passes (référence, montant exact, combinaison). Le lettrage manuel : cochez des lignes dont le solde est 0, puis validez.',
  'help.accounting.lettrage.rules':
    'RG-LT1|Uniquement sur comptes lettrables.\nRG-LT2|Le groupe lettré doit être à 0 (débit = crédit).\nRG-LT3|Délettrage possible tant que la période n’est pas verrouillée.',
  'help.accounting.lettrage.example': 'Ex: Facture 12 000 MAD (D 411) + virement 12 000 MAD (C 411) → code LET-0042, solde 0.',
  'help.accounting.lettrage.guide':
    'Choisir le compte 411 ou 401\nLancer le lettrage automatique\nContrôler les restes\nSélectionner manuellement les lignes à 0\nValider le code de lettrage',
  'help.accounting.lettrage.version': 'v1.0.0',

  'help.accounting.reports.title': 'Rapports comptables',
  'help.accounting.reports.n1': 'Balance, grand livre et journaux pour contrôle et clôture.',
  'help.accounting.reports.body':
    'La balance affiche, par compte, les totaux débit/crédit et le solde de la période.\nLe grand livre détaille les mouvements d’un compte.\nUtilisez ces états avant TVA, lettrage et clôture.',
  'help.accounting.reports.rules':
    'RG-RP1|Les montants reflètent les écritures validées de la période.\nRG-EC1|La balance générale doit être équilibrée (Σ D = Σ C).',
  'help.accounting.reports.example': 'Ex: Balance mars 2026 — 411 solde débiteur 85 400 MAD (impayés clients).',
  'help.accounting.reports.guide':
    'Choisir l’exercice et la période\nOuvrir la balance\nCliquer un compte pour le grand livre\nContrôler les soldes anormaux\nExporter si besoin',
  'help.accounting.reports.version': 'v1.0.0',

  'help.accounting.vat.title': 'Déclaration de TVA',
  'help.accounting.vat.n1': 'Collectée vs déductible du mois, puis déclaration et export EDI.',
  'help.accounting.vat.body':
    'Le récapitulatif reprend la TVA collectée (ventes) et déductible (achats) des écritures du mois.\nDéclarez lorsque les contrôles sont bons : le mois est marqué déclaré.\nL’export EDI produit le fichier XML DGI à déposer (pas d’envoi automatique).',
  'help.accounting.vat.rules':
    'RG-TVA1|Uniquement écritures validées de la période.\nRG-TVA2|Déclaration = gel du mois TVA (annulation possible selon droits).\nRG-TVA3|Export EDI = fichier local, pas de télédéclaration SOAP.',
  'help.accounting.vat.example': 'Ex: Mars 2026 — collectée 18 000, déductible 7 200 → TVA à payer 10 800 MAD.',
  'help.accounting.vat.guide':
    'Sélectionner mois et année\nContrôler collectée / déductible / net\nTraiter les alertes\nDéclarer\nTélécharger l’EDI XML DGI',
  'help.accounting.vat.version': 'v1.0.0',

  'help.accounting.closing.title': 'Clôture d’exercice',
  'help.accounting.closing.n1': 'Contrôles, à-nouveaux (AN) et ouverture de N+1.',
  'help.accounting.closing.body':
    'Avant de clôturer : périodes verrouillées, TVA déclarée, lettrage avancé, immobilisations à jour.\nLa clôture annuelle génère les à-nouveaux (journal AN) vers l’exercice suivant et fige l’exercice.\nL’ouverture de N+1 peut se faire avant la clôture définitive de N.',
  'help.accounting.closing.rules':
    'RG-CL1|Prévisualiser les contrôles avant de clôturer.\nRG-CL2|Clôture annuelle = écriture AN + exercice en lecture seule.\nRG-EX5|Plus de saisie sur un exercice clôturé.',
  'help.accounting.closing.example': 'Ex: Clôture 2025 → AN au 01/01/2026 : 411, 401, 512, capitaux reportés.',
  'help.accounting.closing.guide':
    'Choisir l’exercice\nLire les contrôles (périodes, TVA, banque)\nCorriger les blocages\nOuvrir N+1 si besoin\nClôturer l’année',
  'help.accounting.closing.version': 'v1.0.0',

  'help.accounting.bankRec.title': 'Rapprochement bancaire',
  'help.accounting.bankRec.n1': 'Importer le relevé (CSV / OFX / OCR) et apparier avec le journal BAN.',
  'help.accounting.bankRec.body':
    'Importez le relevé de la banque, puis appariez chaque ligne avec une écriture du journal banque.\nLe rapprochement automatique fait 3 passes (montant+date, référence, montant seul).\nQuand tout est apparié, clôturez le rapprochement : la période peut être marquée rapprochée.',
  'help.accounting.bankRec.rules':
    'RG-RB1|Un rapprochement ouvert par compte / période.\nRG-RB2|Clôture seulement si toutes les lignes sont appariées.\nRG-RB3|Formats : CSV, OFX/QFX, relevés marocains, ou image OCR.',
  'help.accounting.bankRec.example': 'Ex: Virement 12 000 MAD le 12/03 = écriture BAN du même jour / même montant.',
  'help.accounting.bankRec.guide':
    'Choisir le compte banque\nImporter le fichier relevé\nLancer l’appariement auto\nPointer les restes à la main\nTerminer le rapprochement',
  'help.accounting.bankRec.version': 'v1.0.0',

  'help.accounting.exports.title': 'Exports comptables (FEC / CSV)',
  'help.accounting.exports.n1': 'Fichier des écritures comptables pour l’expert ou le contrôle.',
  'help.accounting.exports.body':
    'Le FEC (fichier des écritures comptables) est l’export normalisé de toutes les écritures d’un exercice.\nLe CSV est un export tableau pour Excel.\nRemettez le FEC à votre cabinet ou en cas de contrôle.',
  'help.accounting.exports.rules':
    'RG-FEC1|Export de l’exercice sélectionné, écritures validées.\nRG-FEC2|Ne remplace pas la liasse fiscale officielle.',
  'help.accounting.exports.example': 'Ex: FEC 2026 → fichier texte à transmettre au cabinet.',
  'help.accounting.exports.guide':
    'Choisir l’exercice\nPrévisualiser le nombre d’écritures\nTélécharger le FEC ou le CSV\nTransmettre au cabinet',
  'help.accounting.exports.version': 'v1.0.0',

  'help.accounting.fixedAssets.title': 'Immobilisations',
  'help.accounting.fixedAssets.n1': 'Fiches d’immos et amortissements (linéaire / dégressif).',
  'help.accounting.fixedAssets.body':
    'Enregistrez chaque bien (code, date de mise en service, durée, mode).\nGénérez les dotations du mois : le système calcule l’amortissement et propose l’écriture (681 / 28).\nLinéaire = quote-part égale ; dégressif = plus fort en début de vie.',
  'help.accounting.fixedAssets.rules':
    'RG-IM1|Date de service et durée obligatoires.\nRG-IM2|Dotation mensuelle une seule fois par période.\nRG-IM3|Comptes immo / amortissement / dotation renseignés.',
  'help.accounting.fixedAssets.example': 'Ex: Camionnette 180 000 MAD, 5 ans linéaire → 3 000 MAD / mois (681 / 281).',
  'help.accounting.fixedAssets.guide':
    'Créer la fiche (désignation, dates, durée, mode)\nVérifier les comptes 2 / 28 / 681\nEnregistrer\nGénérer les dotations du mois\nContrôler l’écriture',
  'help.accounting.fixedAssets.version': 'v1.0.0',

  'help.accounting.payroll.title': 'Paie (CNSS / AMO / IGR)',
  'help.accounting.payroll.n1': 'Salariés, bulletins du mois, exports CNSS TXT et XML.',
  'help.accounting.payroll.body':
    'Maintenez le fichier salariés (n° CNSS, salaire de base).\nCalculez les bulletins du mois (CNSS, AMO, IGR selon barèmes paramétrés).\nExportez le fichier CNSS (texte ou XML) pour DAMANCOM — dépôt manuel, pas d’envoi automatique.',
  'help.accounting.payroll.rules':
    'RG-PA1|N° CNSS recommandé pour l’export.\nRG-PA2|Un bulletin par salarié / mois.\nRG-PA3|Export = fichier à déposer, pas de SOAP DAMANCOM.',
  'help.accounting.payroll.example': 'Ex: Salaire 8 000 MAD → CNSS salarié + patronal, AMO, IGR selon barème, net à payer.',
  'help.accounting.payroll.guide':
    'Créer / mettre à jour les salariés\nChoisir le mois\nCalculer les bulletins\nContrôler CNSS / AMO / IGR / net\nExporter TXT ou XML CNSS',
  'help.accounting.payroll.version': 'v1.0.0',

  'help.accounting.ocr.title': 'OCR factures et relevés',
  'help.accounting.ocr.n1': 'Lire un scan, contrôler, puis créer un brouillon FF ou un rapprochement.',
  'help.accounting.ocr.body':
    'Extraire utilise le même parseur Python que Documents / Achats : il classifie d’abord (facture, BL ou relevé), puis extrait ICE/totaux ou les lignes bancaires.\nLes parseurs fournisseurs (Knauf, etc.) ne s’appliquent pas aux relevés.\nEnsuite : facture → Créer brouillon facture F (Achats) ; relevé → Importer en rapprochement ; BL → Documents / Achats.',
  'help.accounting.ocr.rules':
    'RG-OCR1|Résultat à valider : l’OCR peut se tromper.\nRG-OCR2|Images : JPG, PNG, WebP, TIFF, PDF ; relevés : CSV, OFX, texte.\nRG-OCR3|Ne remplace pas la pièce justificative originale.\nRG-FF1|Le brouillon FF reste à contrôler et valider dans Achats.\nRG-OCR4|Type Auto par défaut ; forcer Facture ou Relevé seulement si la détection est fausse.',
  'help.accounting.ocr.example': 'Ex: Scan facture → Extraire (type facture, ICE + TTC) → Créer brouillon FF-884 → ouvrir Achats pour valider.',
  'help.accounting.ocr.guide':
    'Laisser Auto (ou forcer le type)\nJoindre le fichier (ou coller le texte)\nExtraire : le parseur détecte facture / BL / relevé\nFacture : créer le brouillon puis ouvrir Achats\nRelevé : importer vers le rapprochement bancaire',
  'help.accounting.ocr.version': 'v1.2.0',

  'help.accounting.cabinet.title': 'Portail cabinet',
  'help.accounting.cabinet.n1': 'Dossiers des sociétés suivies par l’expert-comptable.',
  'help.accounting.cabinet.body':
    'Le cabinet voit les sociétés liées : exercice, dernières écritures, TVA, clôture.\nUn expert avec le droit Cabinet peut forcer une clôture ou relier une société au dossier.\nLes utilisateurs société restent responsables de la saisie quotidienne.',
  'help.accounting.cabinet.rules':
    'RG-CAB1|Accès limité aux sociétés du dossier.\nRG-CAB2|Force close = droit Cabinet ou Validate.\nRG-SP1|Traçabilité des actions cabinet.',
  'help.accounting.cabinet.example': 'Ex: Dossier EuroBrico SA — exercice 2026 ouvert, TVA février déclarée, 3 écritures en attente.',
  'help.accounting.cabinet.guide':
    'Ouvrir le portail cabinet\nSélectionner une société\nConsulter l’état (TVA, clôture)\nIntervenir seulement si mandaté\nLaisser une trace (commentaire / action)',
  'help.accounting.cabinet.version': 'v1.0.0',

  'help.field.accounting.account.title': 'Compte comptable',
  'help.field.accounting.account.n1': 'N° du plan (ex. 411, 401, 512, 701).',
  'help.field.accounting.account.body': 'Saisissez un compte existant du plan. Classes usuelles : 4 tiers, 5 trésorerie, 6 charges, 7 produits.',
  'help.field.accounting.account.rules': 'RG-EC2|Le compte doit exister et être actif.',
  'help.field.accounting.account.version': 'v1.1.0',

  'help.field.accounting.journal.title': 'Journal',
  'help.field.accounting.journal.n1': 'Code de ventilation : ACH, VEN, BAN, CAIS, OD, AN.',
  'help.field.accounting.journal.body':
    'Le journal indique le circuit de l’écriture. La contrepartie (compte 5) est utile pour BAN et CAIS.',
  'help.field.accounting.journal.version': 'v1.0.0',

  'help.field.accounting.period.title': 'Période',
  'help.field.accounting.period.n1': 'Mois de l’exercice : ouvert ou verrouillé.',
  'help.field.accounting.period.body':
    'Une période verrouillée refuse toute nouvelle écriture. Déverrouillez seulement pour une correction justifiée.',
  'help.field.accounting.period.rules': 'RG-PM2|Période fermée = saisie bloquée.',
  'help.field.accounting.period.version': 'v1.0.0',

  'help.field.accounting.lettrageAccount.title': 'Compte à lettrer',
  'help.field.accounting.lettrageAccount.n1': 'Compte de tiers lettrable (411 clients, 401 fournisseurs).',
  'help.field.accounting.lettrageAccount.body':
    'Choisissez un compte marqué lettrable dans le plan. Les lignes non lettrées apparaissent alors pour appariement.',
  'help.field.accounting.lettrageAccount.version': 'v1.0.0',

  'help.field.accounting.vatMonth.title': 'Mois de TVA',
  'help.field.accounting.vatMonth.n1': 'Période fiscale de la déclaration (mois + année).',
  'help.field.accounting.vatMonth.body':
    'La déclaration agrège la TVA collectée et déductible des écritures validées de ce mois.',
  'help.field.accounting.vatMonth.version': 'v1.0.0',

  'help.field.payroll.cnss.title': 'N° CNSS',
  'help.field.payroll.cnss.n1': 'Identifiant salarié auprès de la CNSS.',
  'help.field.payroll.cnss.body':
    'Obligatoire pour un export DAMANCOM propre. Format habituel : numéro d’immatriculation salarié.',
  'help.field.payroll.cnss.version': 'v1.0.0',

  'help.field.accounting.ocrFile.title': 'Fichier à lire',
  'help.field.accounting.ocrFile.n1': 'Scan, PDF, CSV ou OFX selon le mode.',
  'help.field.accounting.ocrFile.body':
    'Facture ou BL : image ou PDF. Relevé : CSV, OFX, image ou PDF. En Auto, le parseur Python choisit le type.',
  'help.field.accounting.ocrFile.version': 'v1.0.0',

  'help.field.accounting.bankFile.title': 'Fichier relevé',
  'help.field.accounting.bankFile.n1': 'CSV, OFX ou image du relevé bancaire.',
  'help.field.accounting.bankFile.body':
    'Importez le fichier fourni par la banque. Les formats marocains (CSV colonnes date/libellé/montant) sont reconnus.',
  'help.field.accounting.bankFile.version': 'v1.0.0'
};

export const HELP_ACCOUNTING_NL: HelpDict = {
  'help.accounting.tabs.title': 'Journaal',
  'help.accounting.tabs.n1': 'Alle boekingen (verkoop, aankoop, betalingen, manueel).',
  'help.accounting.tabs.body':
    'Dit scherm toont automatische boekingen (facturen, creditnota’s, betalingen, aankopen) en manuele invoer.\nFilter op journaal, stuktype en periode.\nAndere boekhoudmenu’s (rekeningenstelsel, journalen, boekjaren, lettrage, rapporten, btw, afsluiting, bank, export, vaste activa, loon, OCR, kantoor) staan in het linkermenu.',
  'help.accounting.tabs.rules':
    'RG-EX3|Geen boeking buiten een open boekjaar.\nRG-EX5|Afgesloten boekjaar = alleen raadplegen.\nRG-PM2|Gesloten periode = invoer geblokkeerd.\nRG-EC1|Manuele boeking: debet = credit.\nRG-SP1|Functiescheiding bij gevoelige validaties.',
  'help.accounting.tabs.example': 'Vb: Factuur FAC-2026-0012 → VEN 411 / 701 / 44571.',
  'help.accounting.tabs.guide':
    'Periode en journaal filteren\nBoeking openen om regels te zien\nManuele boeking indien nodig\nDebet / credit in evenwicht\nOpslaan',
  'help.accounting.tabs.version': 'v1.1.0',

  'help.accounting.newEntry.title': 'Handmatige boeking',
  'help.accounting.newEntry.n1': 'Vrije invoer: journaal, datum, omschrijving en D/C-regels.',
  'help.accounting.newEntry.body':
    'Gebruik dit formulier voor een bewerking die niet uit een factuur komt (diverse posten, regularisatie).\nKies journaal, datum (open periode) en minstens twee regels.\nDebet- en credittotalen moeten gelijk zijn.',
  'help.accounting.newEntry.rules':
    'RG-EC1|Σ debet = Σ credit.\nRG-EX3|Datum in open boekjaar/periode.\nRG-EC2|Rekening bestaat en is actief.',
  'help.accounting.newEntry.example': 'Vb: OD — 658 diverse kosten 1 200 MAD (D) / 512 bank 1 200 MAD (C).',
  'help.accounting.newEntry.guide':
    'Journaal en datum kiezen\nOmschrijving\nMinstens 2 regels\nEvenwicht controleren\nOpslaan',
  'help.accounting.newEntry.version': 'v1.1.0',

  'help.accounting.chart.title': 'Rekeningenstelsel',
  'help.accounting.chart.n1': 'Referentie van rekeningen (PCG / PCM): klassen, types, lettrable.',
  'help.accounting.chart.body':
    'Het rekeningenstelsel is het woordenboek van de boekhouding.\nStandaard PCG Europa (klassen 1–7). U kunt rekeningen toevoegen, lettrable maken (411/401) of deactiveren.\nVerwijder geen rekening met bewegingen: deactiveer ze.',
  'help.accounting.chart.rules':
    'RG-PC1|Uniek nummer per vennootschap.\nRG-PC2|Klasse 1–7 in lijn met het eerste cijfer.\nRG-PC3|Bewogen rekening: deactiveren, niet wissen.\nRG-LT1|Lettrable = derdenrekeningen (411 / 401).',
  'help.accounting.chart.example': 'Vb: 411100 Klanten — klasse 4, Asset, lettrable; 701100 Verkoop — klasse 7, Income.',
  'help.accounting.chart.guide':
    'Filteren op klasse of nummer zoeken\nRekening aanmaken\nLettrable aanvinken voor derden\nOpslaan\nNummer gebruiken in boekingen',
  'help.accounting.chart.version': 'v1.0.0',

  'help.accounting.journals.title': 'Boekhoudkundige journalen',
  'help.accounting.journals.n1': 'Codes ACH, VEN, BAN, CAIS, OD, AN.',
  'help.accounting.journals.body':
    'Een journaal groepeert boekingen van hetzelfde circuit: aankopen, verkopen, bank, kas, diverse, beginbalans.\nDe tegenrekening (bv. 512) wordt voorgesteld op kas/bank-journalen.',
  'help.accounting.journals.rules':
    'RG-JN1|Unieke journaalcode.\nRG-JN2|Tegenrekening nuttig voor BAN / CAIS.\nRG-JN3|Automatische boekingen gebruiken het journaal van het document.',
  'help.accounting.journals.example': 'Vb: Journaal BAN « Banque BMCE » — tegenrekening 512100.',
  'help.accounting.journals.guide':
    'Journalen openen\nCode aanmaken\nOmschrijving\nTegenrekening indien bank/kas\nOpslaan',
  'help.accounting.journals.version': 'v1.0.0',

  'help.accounting.fiscalYears.title': 'Boekjaren en perioden',
  'help.accounting.fiscalYears.n1': 'Boekjaar openen (12 perioden) en een maand vergrendelen.',
  'help.accounting.fiscalYears.body':
    'Een boekjaar = fiscaal jaar. Bij opening worden 12 maandperioden aangemaakt.\nVergrendel een afgesloten maand om nieuwe invoer te blokkeren.',
  'help.accounting.fiscalYears.rules':
    'RG-EX1|In principe één open boekjaar.\nRG-EX3|Invoer alleen in een open periode.\nRG-PM2|Vergrendelde periode = geen nieuwe boeking.\nRG-EX5|Afgesloten boekjaar = alleen lezen.',
  'help.accounting.fiscalYears.example': 'Vb: Boekjaar 2026 open, januari en februari vergrendeld na btw-aangifte.',
  'help.accounting.fiscalYears.guide':
    'Boekjaar openen (begin/eind)\n12 perioden controleren\nMaand vergrendelen na controles\nAlleen ontgrendelen voor een gerechtvaardigde correctie',
  'help.accounting.fiscalYears.version': 'v1.0.0',

  'help.accounting.lettrage.title': 'Lettrage (derdenafstemming)',
  'help.accounting.lettrage.n1': 'Facturen en betalingen koppelen op 411 / 401.',
  'help.accounting.lettrage.body':
    'Lettrage koppelt regels van dezelfde derdenrekening tot saldo 0.\nAutomatisch: 3 passes (referentie, exact bedrag, combinatie). Manueel: vink regels met saldo 0 aan.',
  'help.accounting.lettrage.rules':
    'RG-LT1|Alleen lettrable rekeningen.\nRG-LT2|Groep moet op 0 staan.\nRG-LT3|Ontletteren mogelijk zolang de periode open is.',
  'help.accounting.lettrage.example': 'Vb: Factuur 12 000 MAD (D 411) + overschrijving 12 000 MAD (C 411) → LET-0042.',
  'help.accounting.lettrage.guide':
    'Rekening 411 of 401 kiezen\nAutomatisch lettrage starten\nResten controleren\nManueel regels op 0 selecteren\nCode bevestigen',
  'help.accounting.lettrage.version': 'v1.0.0',

  'help.accounting.reports.title': 'Boekhoudrapporten',
  'help.accounting.reports.n1': 'Proefbalans, grootboek en journalen.',
  'help.accounting.reports.body':
    'De proefbalans toont per rekening debet/credit en saldo.\nHet grootboek detailleert de bewegingen.\nGebruik deze staten vóór btw, lettrage en afsluiting.',
  'help.accounting.reports.rules':
    'RG-RP1|Bedragen = gevalideerde boekingen van de periode.\nRG-EC1|Algemene balans in evenwicht (Σ D = Σ C).',
  'help.accounting.reports.example': 'Vb: Proefbalans maart 2026 — 411 debetsaldo 85 400 MAD.',
  'help.accounting.reports.guide':
    'Boekjaar en periode kiezen\nProefbalans openen\nOp een rekening klikken voor het grootboek\nAbnormale saldi controleren\nDesgewenst exporteren',
  'help.accounting.reports.version': 'v1.0.0',

  'help.accounting.vat.title': 'Btw-aangifte',
  'help.accounting.vat.n1': 'Verschuldigde vs aftrekbare btw van de maand, daarna EDI.',
  'help.accounting.vat.body':
    'Het overzicht neemt geïnde en aftrekbare btw van de maand.\nDeclareer na controle. EDI levert het DGI-XML-bestand (geen automatische verzending).',
  'help.accounting.vat.rules':
    'RG-TVA1|Alleen gevalideerde boekingen van de periode.\nRG-TVA2|Aangifte bevriest de btw-maand.\nRG-TVA3|EDI = lokaal bestand, geen SOAP.',
  'help.accounting.vat.example': 'Vb: Maart 2026 — geïnd 18 000, aftrekbaar 7 200 → te betalen 10 800 MAD.',
  'help.accounting.vat.guide':
    'Maand en jaar kiezen\nGeïnd / aftrekbaar / netto controleren\nWaarschuwingen behandelen\nAangeven\nDGI-XML downloaden',
  'help.accounting.vat.version': 'v1.0.0',

  'help.accounting.closing.title': 'Boekjaarafsluiting',
  'help.accounting.closing.n1': 'Controles, beginbalans (AN) en opening N+1.',
  'help.accounting.closing.body':
    'Voor afsluiting: perioden vergrendeld, btw aangegeven, lettrage gevorderd, activa bijgewerkt.\nJaarafsluiting maakt beginbalans (AN) naar het volgende boekjaar.',
  'help.accounting.closing.rules':
    'RG-CL1|Controles bekijken vóór afsluiten.\nRG-CL2|Jaarafsluiting = AN-boeking + alleen-lezen.\nRG-EX5|Geen invoer meer op een afgesloten boekjaar.',
  'help.accounting.closing.example': 'Vb: Afsluiting 2025 → AN op 01/01/2026: 411, 401, 512, overgedragen kapitaal.',
  'help.accounting.closing.guide':
    'Boekjaar kiezen\nControles lezen\nBlokkades corrigeren\nN+1 openen indien nodig\nJaar afsluiten',
  'help.accounting.closing.version': 'v1.0.0',

  'help.accounting.bankRec.title': 'Bankafstemming',
  'help.accounting.bankRec.n1': 'Rekeningafschrift importeren (CSV / OFX / OCR) en koppelen aan BAN.',
  'help.accounting.bankRec.body':
    'Importeer het bankafschrift en koppel elke lijn aan een bankboeking.\nAutomatisch: 3 passes. Sluit af wanneer alles gekoppeld is.',
  'help.accounting.bankRec.rules':
    'RG-RB1|Eén open afstemming per rekening/periode.\nRG-RB2|Afsluiten alleen als alle lijnen gekoppeld zijn.\nRG-RB3|Formaten: CSV, OFX/QFX, Marokkaanse afschriften, of OCR-beeld.',
  'help.accounting.bankRec.example': 'Vb: Overschrijving 12 000 MAD op 12/03 = BAN-boeking zelfde dag / bedrag.',
  'help.accounting.bankRec.guide':
    'Bankrekening kiezen\nBestand importeren\nAuto-koppeling starten\nResten manueel wijzen\nAfstemming afronden',
  'help.accounting.bankRec.version': 'v1.0.0',

  'help.accounting.exports.title': 'Boekhoudexport (FEC / CSV)',
  'help.accounting.exports.n1': 'Bestand van boekingen voor de expert of controle.',
  'help.accounting.exports.body':
    'De FEC is de gestandaardiseerde export van alle boekingen van een boekjaar.\nCSV is een Excel-export. Bezorg de FEC aan uw kantoor.',
  'help.accounting.exports.rules':
    'RG-FEC1|Export van het gekozen boekjaar, gevalideerde boekingen.\nRG-FEC2|Vervangt niet de officiële jaarrekening.',
  'help.accounting.exports.example': 'Vb: FEC 2026 → tekstbestand voor het kantoor.',
  'help.accounting.exports.guide':
    'Boekjaar kiezen\nAantal boekingen bekijken\nFEC of CSV downloaden\nAan het kantoor bezorgen',
  'help.accounting.exports.version': 'v1.0.0',

  'help.accounting.fixedAssets.title': 'Vaste activa',
  'help.accounting.fixedAssets.n1': 'Actiefiches en afschrijvingen (lineair / degressief).',
  'help.accounting.fixedAssets.body':
    'Registreer elk goed (code, indienststellingsdatum, duur, modus).\nGenereer de maandelijkse dotaties (681 / 28).',
  'help.accounting.fixedAssets.rules':
    'RG-IM1|Indienststellingsdatum en duur verplicht.\nRG-IM2|Eén dotatie per periode.\nRG-IM3|Rekeningen 2 / 28 / 681 ingevuld.',
  'help.accounting.fixedAssets.example': 'Vb: Bestelwagen 180 000 MAD, 5 jaar lineair → 3 000 MAD / maand.',
  'help.accounting.fixedAssets.guide':
    'Fiche aanmaken\nRekeningen controleren\nOpslaan\nMaanddotaties genereren\nBoeking controleren',
  'help.accounting.fixedAssets.version': 'v1.0.0',

  'help.accounting.payroll.title': 'Loon (CNSS / AMO / IGR)',
  'help.accounting.payroll.n1': 'Werknemers, maandfiches, CNSS TXT- en XML-export.',
  'help.accounting.payroll.body':
    'Onderhoud het personeelsbestand (CNSS-nummer, basissalaris).\nBereken de maandfiches. Exporteer CNSS voor DAMANCOM (manuele deponering).',
  'help.accounting.payroll.rules':
    'RG-PA1|CNSS-nummer aanbevolen voor export.\nRG-PA2|Eén fiche per werknemer / maand.\nRG-PA3|Export = bestand, geen SOAP.',
  'help.accounting.payroll.example': 'Vb: Salaris 8 000 MAD → CNSS, AMO, IGR, netto.',
  'help.accounting.payroll.guide':
    'Werknemers bijwerken\nMaand kiezen\nFiches berekenen\nCNSS / AMO / IGR / netto controleren\nTXT of XML exporteren',
  'help.accounting.payroll.version': 'v1.0.0',

  'help.accounting.ocr.title': 'OCR facturen en afschriften',
  'help.accounting.ocr.n1': 'Scan lezen, controleren, daarna concept-FF of bankafstemming.',
  'help.accounting.ocr.body':
    'Extractie gebruikt dezelfde Python-parser als Documenten / Aankopen: eerst classificatie (factuur, LB of uittreksel), daarna ICE/totalen of bankregels.\nLeveranciersparsers (Knauf, enz.) gelden niet voor uittreksels.\nDaarna: factuur → conceptfactuur F (Aankopen); uittreksel → afstemming; LB → Documenten / Aankopen.',
  'help.accounting.ocr.rules':
    'RG-OCR1|Resultaat valideren: OCR kan zich vergissen.\nRG-OCR2|Beelden: JPG, PNG, WebP, TIFF, PDF; afschriften: CSV, OFX, tekst.\nRG-OCR3|Vervangt niet het originele bewijsstuk.\nRG-FF1|Het FF-concept blijft te controleren en valideren in Aankopen.\nRG-OCR4|Type Auto standaard; Factuur of Uittreksel forceren alleen bij foute detectie.',
  'help.accounting.ocr.example': 'Vb: Scan factuur → Extractie (type factuur) → concept FF-884 → Aankopen openen om te valideren.',
  'help.accounting.ocr.guide':
    'Auto laten (of type forceren)\nBestand bijvoegen\nExtraheren: parser herkent factuur / LB / uittreksel\nFactuur: concept aanmaken en Aankopen openen\nUittreksel: importeren naar bankafstemming',
  'help.accounting.ocr.version': 'v1.2.0',

  'help.accounting.cabinet.title': 'Kantoorportaal',
  'help.accounting.cabinet.n1': 'Dossiers van vennootschappen opgevolgd door de expert.',
  'help.accounting.cabinet.body':
    'Het kantoor ziet gekoppelde vennootschappen: boekjaar, laatste boekingen, btw, afsluiting.\nMet recht Cabinet kan een expert een afsluiting forceren of een vennootschap koppelen.',
  'help.accounting.cabinet.rules':
    'RG-CAB1|Toegang beperkt tot dossiervennootschappen.\nRG-CAB2|Force close = recht Cabinet of Validate.\nRG-SP1|Traceerbaarheid van kantooracties.',
  'help.accounting.cabinet.example': 'Vb: Dossier EuroBrico SA — boekjaar 2026 open, btw februari aangegeven.',
  'help.accounting.cabinet.guide':
    'Portaal openen\nVennootschap selecteren\nStatus raadplegen\nAlleen ingrijpen indien gemachtigd\nSpoor achterlaten',
  'help.accounting.cabinet.version': 'v1.0.0',

  'help.field.accounting.account.title': 'Grootboekrekening',
  'help.field.accounting.account.n1': 'Nummer uit het stelsel (bv. 411, 401, 512, 701).',
  'help.field.accounting.account.body': 'Gebruik een bestaande rekening. Klassen: 4 derden, 5 liquiditeiten, 6 kosten, 7 opbrengsten.',
  'help.field.accounting.account.rules': 'RG-EC2|Rekening moet bestaan en actief zijn.',
  'help.field.accounting.account.version': 'v1.1.0',

  'help.field.accounting.journal.title': 'Journaal',
  'help.field.accounting.journal.n1': 'Code: ACH, VEN, BAN, CAIS, OD, AN.',
  'help.field.accounting.journal.body': 'Het journaal geeft het circuit van de boeking. Tegenrekening (5) is nuttig voor BAN en CAIS.',
  'help.field.accounting.journal.version': 'v1.0.0',

  'help.field.accounting.period.title': 'Periode',
  'help.field.accounting.period.n1': 'Maand van het boekjaar: open of vergrendeld.',
  'help.field.accounting.period.body': 'Een vergrendelde periode weigert nieuwe boekingen.',
  'help.field.accounting.period.rules': 'RG-PM2|Gesloten periode = invoer geblokkeerd.',
  'help.field.accounting.period.version': 'v1.0.0',

  'help.field.accounting.lettrageAccount.title': 'Rekening om te letteren',
  'help.field.accounting.lettrageAccount.n1': 'Lettrable derdenrekening (411 / 401).',
  'help.field.accounting.lettrageAccount.body': 'Kies een lettrable rekening. Niet-geletterde regels verschijnen dan.',
  'help.field.accounting.lettrageAccount.version': 'v1.0.0',

  'help.field.accounting.vatMonth.title': 'Btw-maand',
  'help.field.accounting.vatMonth.n1': 'Fiscale periode van de aangifte (maand + jaar).',
  'help.field.accounting.vatMonth.body': 'De aangifte aggregeert geïnde en aftrekbare btw van die maand.',
  'help.field.accounting.vatMonth.version': 'v1.0.0',

  'help.field.payroll.cnss.title': 'CNSS-nummer',
  'help.field.payroll.cnss.n1': 'Werknemersidentificatie bij de CNSS.',
  'help.field.payroll.cnss.body': 'Aanbevolen voor een nette DAMANCOM-export.',
  'help.field.payroll.cnss.version': 'v1.0.0',

  'help.field.accounting.ocrFile.title': 'Te lezen bestand',
  'help.field.accounting.ocrFile.n1': 'Scan, PDF, CSV of OFX volgens de modus.',
  'help.field.accounting.ocrFile.body': 'Factuur of LB: beeld of PDF. Uittreksel: CSV, OFX, beeld of PDF. In Auto kiest de Python-parser het type.',
  'help.field.accounting.ocrFile.version': 'v1.0.0',

  'help.field.accounting.bankFile.title': 'Afschriftbestand',
  'help.field.accounting.bankFile.n1': 'CSV, OFX of beeld van het bankafschrift.',
  'help.field.accounting.bankFile.body': 'Importeer het bestand van de bank. Marokkaanse CSV-kolommen worden herkend.',
  'help.field.accounting.bankFile.version': 'v1.0.0'
};

export const HELP_ACCOUNTING_EN: HelpDict = {
  'help.accounting.tabs.title': 'Journal entries',
  'help.accounting.tabs.n1': 'All entries (sales, purchases, payments, manual).',
  'help.accounting.tabs.body':
    'This screen lists entries generated from invoices, credit notes, payments and purchases, plus manual journals.\nFilter by journal, document type and period.\nOther Accounting menus (chart, journals, fiscal years, matching, reports, VAT, closing, bank, exports, assets, payroll, OCR, firm portal) are in the left menu.',
  'help.accounting.tabs.rules':
    'RG-EX3|No posting outside an open fiscal year.\nRG-EX5|Closed year = enquiry only.\nRG-PM2|Locked period = posting blocked.\nRG-EC1|Manual entry: debit = credit.\nRG-SP1|Segregation of duties for sensitive approvals.',
  'help.accounting.tabs.example': 'E.g. Invoice FAC-2026-0012 → Sales 411 / 701 / 44571.',
  'help.accounting.tabs.guide':
    'Filter period and journal\nOpen an entry to see lines\nCreate a manual entry if needed\nBalance debit / credit\nSave',
  'help.accounting.tabs.version': 'v1.1.0',

  'help.accounting.newEntry.title': 'Manual journal entry',
  'help.accounting.newEntry.n1': 'Free entry: journal, date, description and debit/credit lines.',
  'help.accounting.newEntry.body':
    'Use this form for an operation not generated by an invoice (miscellaneous, adjustment).\nChoose journal, date (open period) and at least two lines.\nDebit and credit totals must match before save.',
  'help.accounting.newEntry.rules':
    'RG-EC1|Σ debit = Σ credit.\nRG-EX3|Date in an open year/period.\nRG-EC2|Account exists and is active.',
  'help.accounting.newEntry.example': 'E.g. OD — 658 miscellaneous 1,200 MAD (Dr) / 512 bank 1,200 MAD (Cr).',
  'help.accounting.newEntry.guide':
    'Choose journal and date\nEnter description\nAdd at least 2 lines\nCheck balance\nSave',
  'help.accounting.newEntry.version': 'v1.1.0',

  'help.accounting.chart.title': 'Chart of accounts',
  'help.accounting.chart.n1': 'Account master (PCG / PCM): classes, types, matchable.',
  'help.accounting.chart.body':
    'The chart is the accounting dictionary: every journal line points to an account number.\nDefault is European PCG (classes 1–7). You can add accounts, mark them matchable (411/401) or deactivate them.\nDo not delete a posted account: deactivate it.',
  'help.accounting.chart.rules':
    'RG-PC1|Unique number per company.\nRG-PC2|Class 1–7 consistent with the first digit.\nRG-PC3|Posted account: deactivate, do not delete.\nRG-LT1|Matchable = third-party accounts (typically 411 / 401).',
  'help.accounting.chart.example': 'E.g. 411100 Customers — class 4, Asset, matchable; 701100 Sales — class 7, Income.',
  'help.accounting.chart.guide':
    'Filter by class or search a number\nCreate an account\nTick Matchable for third parties\nSave\nUse the number on entries',
  'help.accounting.chart.version': 'v1.0.0',

  'help.accounting.journals.title': 'Accounting journals',
  'help.accounting.journals.n1': 'Codes ACH, VEN, BAN, CAIS, OD, AN.',
  'help.accounting.journals.body':
    'A journal groups entries of the same flow: purchases, sales, bank, cash, miscellaneous, opening balances.\nThe counterpart account (e.g. 512) is suggested on cash/bank journals.',
  'help.accounting.journals.rules':
    'RG-JN1|Unique journal code.\nRG-JN2|Counterpart useful for BAN / CAIS.\nRG-JN3|Automatic entries use the document journal.',
  'help.accounting.journals.example': 'E.g. Journal BAN “Banque BMCE” — counterpart 512100.',
  'help.accounting.journals.guide':
    'Open Journals\nCreate a code\nEnter the label\nSet counterpart for bank/cash\nSave',
  'help.accounting.journals.version': 'v1.0.0',

  'help.accounting.fiscalYears.title': 'Fiscal years and periods',
  'help.accounting.fiscalYears.n1': 'Open a year (12 periods) and lock a month.',
  'help.accounting.fiscalYears.body':
    'A fiscal year is typically 1 Jan–31 Dec. Opening creates 12 monthly periods.\nLock a closed month to block new postings. A closed year is enquiry only.',
  'help.accounting.fiscalYears.rules':
    'RG-EX1|Normally one open year at a time.\nRG-EX3|Posting only in an open period.\nRG-PM2|Locked period = no new entry.\nRG-EX5|Closed year = read-only.',
  'help.accounting.fiscalYears.example': 'E.g. Year 2026 open, January and February locked after the VAT return.',
  'help.accounting.fiscalYears.guide':
    'Open a year (start / end dates)\nCheck the 12 periods\nLock the month after controls\nUnlock only for a justified correction',
  'help.accounting.fiscalYears.version': 'v1.0.0',

  'help.accounting.lettrage.title': 'Matching (third-party reconciliation)',
  'help.accounting.lettrage.n1': 'Pair invoices and payments on 411 / 401.',
  'help.accounting.lettrage.body':
    'Matching links lines on the same third-party account until the balance is zero.\nAutomatic matching runs 3 passes (reference, exact amount, combination). Manual: tick lines that net to 0.',
  'help.accounting.lettrage.rules':
    'RG-LT1|Matchable accounts only.\nRG-LT2|Matched group must net to 0.\nRG-LT3|Unmatch allowed while the period is open.',
  'help.accounting.lettrage.example': 'E.g. Invoice 12,000 MAD (Dr 411) + transfer 12,000 MAD (Cr 411) → LET-0042.',
  'help.accounting.lettrage.guide':
    'Pick account 411 or 401\nRun automatic matching\nReview remainders\nSelect lines that net to 0\nConfirm the matching code',
  'help.accounting.lettrage.version': 'v1.0.0',

  'help.accounting.reports.title': 'Accounting reports',
  'help.accounting.reports.n1': 'Trial balance, general ledger and journals.',
  'help.accounting.reports.body':
    'The trial balance shows debit/credit totals and the period balance per account.\nThe general ledger details movements.\nUse these statements before VAT, matching and closing.',
  'help.accounting.reports.rules':
    'RG-RP1|Amounts reflect posted entries of the period.\nRG-EC1|The trial balance must balance (Σ Dr = Σ Cr).',
  'help.accounting.reports.example': 'E.g. March 2026 trial balance — 411 debit 85,400 MAD (unpaid customers).',
  'help.accounting.reports.guide':
    'Choose year and period\nOpen the trial balance\nClick an account for the ledger\nCheck unusual balances\nExport if needed',
  'help.accounting.reports.version': 'v1.0.0',

  'help.accounting.vat.title': 'VAT return',
  'help.accounting.vat.n1': 'Output vs input VAT for the month, then EDI export.',
  'help.accounting.vat.body':
    'The summary takes output VAT (sales) and input VAT (purchases) from the month’s entries.\nDeclare when checks are OK. EDI produces the DGI XML file (no automatic filing).',
  'help.accounting.vat.rules':
    'RG-TVA1|Posted entries of the period only.\nRG-TVA2|Declaring freezes the VAT month.\nRG-TVA3|EDI = local file, no SOAP filing.',
  'help.accounting.vat.example': 'E.g. March 2026 — output 18,000, input 7,200 → VAT due 10,800 MAD.',
  'help.accounting.vat.guide':
    'Select month and year\nCheck output / input / net\nClear alerts\nDeclare\nDownload DGI XML',
  'help.accounting.vat.version': 'v1.0.0',

  'help.accounting.closing.title': 'Year-end closing',
  'help.accounting.closing.n1': 'Checks, opening balances (AN) and year N+1.',
  'help.accounting.closing.body':
    'Before closing: periods locked, VAT declared, matching progressed, assets up to date.\nAnnual close posts opening balances (journal AN) to the next year and freezes the year.',
  'help.accounting.closing.rules':
    'RG-CL1|Preview checks before closing.\nRG-CL2|Annual close = AN entry + read-only year.\nRG-EX5|No posting on a closed year.',
  'help.accounting.closing.example': 'E.g. Close 2025 → AN on 01/01/2026: 411, 401, 512, retained earnings.',
  'help.accounting.closing.guide':
    'Select the year\nRead the checks\nFix blockers\nOpen N+1 if needed\nClose the year',
  'help.accounting.closing.version': 'v1.0.0',

  'help.accounting.bankRec.title': 'Bank reconciliation',
  'help.accounting.bankRec.n1': 'Import the statement (CSV / OFX / OCR) and match BAN entries.',
  'help.accounting.bankRec.body':
    'Import the bank statement, then match each line to a bank journal entry.\nAuto-match runs 3 passes. Complete when every line is matched.',
  'help.accounting.bankRec.rules':
    'RG-RB1|One open reconciliation per account/period.\nRG-RB2|Complete only if all lines are matched.\nRG-RB3|Formats: CSV, OFX/QFX, Moroccan statements, or OCR image.',
  'help.accounting.bankRec.example': 'E.g. Transfer 12,000 MAD on 12/03 = BAN entry same day / amount.',
  'help.accounting.bankRec.guide':
    'Choose the bank account\nImport the file\nRun auto-match\nPoint remaining lines manually\nComplete the reconciliation',
  'help.accounting.bankRec.version': 'v1.0.0',

  'help.accounting.exports.title': 'Accounting exports (FEC / CSV)',
  'help.accounting.exports.n1': 'Journal file for the accountant or an audit.',
  'help.accounting.exports.body':
    'FEC is the standardised export of all entries for a year.\nCSV is a spreadsheet export. Send the FEC to your firm.',
  'help.accounting.exports.rules':
    'RG-FEC1|Export of the selected year, posted entries.\nRG-FEC2|Does not replace the official tax pack.',
  'help.accounting.exports.example': 'E.g. FEC 2026 → text file for the accounting firm.',
  'help.accounting.exports.guide':
    'Select the year\nPreview entry count\nDownload FEC or CSV\nSend to the firm',
  'help.accounting.exports.version': 'v1.0.0',

  'help.accounting.fixedAssets.title': 'Fixed assets',
  'help.accounting.fixedAssets.n1': 'Asset cards and depreciation (straight-line / declining).',
  'help.accounting.fixedAssets.body':
    'Register each asset (code, in-service date, life, method).\nGenerate monthly depreciation (681 / 28).',
  'help.accounting.fixedAssets.rules':
    'RG-IM1|In-service date and life required.\nRG-IM2|One charge per period.\nRG-IM3|Asset / accum. / expense accounts set.',
  'help.accounting.fixedAssets.example': 'E.g. Van 180,000 MAD, 5 years straight-line → 3,000 MAD / month.',
  'help.accounting.fixedAssets.guide':
    'Create the card\nCheck accounts\nSave\nGenerate monthly charges\nReview the entry',
  'help.accounting.fixedAssets.version': 'v1.0.0',

  'help.accounting.payroll.title': 'Payroll (CNSS / AMO / IGR)',
  'help.accounting.payroll.n1': 'Employees, monthly slips, CNSS TXT and XML exports.',
  'help.accounting.payroll.body':
    'Maintain employees (CNSS number, base salary).\nCompute monthly slips. Export CNSS for DAMANCOM (manual filing).',
  'help.accounting.payroll.rules':
    'RG-PA1|CNSS number recommended for export.\nRG-PA2|One slip per employee / month.\nRG-PA3|Export = file, no SOAP.',
  'help.accounting.payroll.example': 'E.g. Salary 8,000 MAD → CNSS, AMO, IGR, net pay.',
  'help.accounting.payroll.guide':
    'Update employees\nSelect the month\nCompute slips\nCheck CNSS / AMO / IGR / net\nExport TXT or XML',
  'help.accounting.payroll.version': 'v1.0.0',

  'help.accounting.ocr.title': 'OCR invoices and statements',
  'help.accounting.ocr.n1': 'Read a scan, review, then create a draft SI or a bank rec.',
  'help.accounting.ocr.body':
    'Extract uses the same Python parser as Documents / Purchases: it classifies first (invoice, DN or statement), then extracts ICE/totals or bank lines.\nSupplier parsers (Knauf, etc.) are not applied to statements.\nNext: invoice → Create supplier invoice draft (Purchases); statement → Import into reconciliation; DN → Documents / Purchases.',
  'help.accounting.ocr.rules':
    'RG-OCR1|Validate the result: OCR can misread.\nRG-OCR2|Images: JPG, PNG, WebP, TIFF, PDF; statements: CSV, OFX, text.\nRG-OCR3|Does not replace the original document.\nRG-FF1|The SI draft still needs review and validation in Purchases.\nRG-OCR4|Default type is Auto; force Invoice or Statement only if detection is wrong.',
  'help.accounting.ocr.example': 'E.g. Invoice scan → Extract (type invoice, ICE + TTC) → draft FF-884 → open Purchases to validate.',
  'help.accounting.ocr.guide':
    'Leave Auto (or force the type)\nAttach the file (or paste text)\nExtract: the parser detects invoice / DN / statement\nInvoice: create the draft then open Purchases\nStatement: import into bank reconciliation',
  'help.accounting.ocr.version': 'v1.2.0',

  'help.accounting.cabinet.title': 'Accounting firm portal',
  'help.accounting.cabinet.n1': 'Company files followed by the accountant.',
  'help.accounting.cabinet.body':
    'The firm sees linked companies: year, latest entries, VAT, closing.\nWith Cabinet rights an expert can force-close or link a company.',
  'help.accounting.cabinet.rules':
    'RG-CAB1|Access limited to dossier companies.\nRG-CAB2|Force close = Cabinet or Validate right.\nRG-SP1|Traceability of firm actions.',
  'help.accounting.cabinet.example': 'E.g. EuroBrico SA file — 2026 open, February VAT declared.',
  'help.accounting.cabinet.guide':
    'Open the firm portal\nSelect a company\nReview status\nAct only if mandated\nLeave an audit trail',
  'help.accounting.cabinet.version': 'v1.0.0',

  'help.field.accounting.account.title': 'GL account',
  'help.field.accounting.account.n1': 'Number from the chart (e.g. 411, 401, 512, 701).',
  'help.field.accounting.account.body': 'Use an existing account. Classes: 4 third parties, 5 cash, 6 expenses, 7 income.',
  'help.field.accounting.account.rules': 'RG-EC2|Account must exist and be active.',
  'help.field.accounting.account.version': 'v1.1.0',

  'help.field.accounting.journal.title': 'Journal',
  'help.field.accounting.journal.n1': 'Code: ACH, VEN, BAN, CAIS, OD, AN.',
  'help.field.accounting.journal.body': 'The journal is the posting flow. Counterpart (class 5) is useful for BAN and CAIS.',
  'help.field.accounting.journal.version': 'v1.0.0',

  'help.field.accounting.period.title': 'Period',
  'help.field.accounting.period.n1': 'Month of the fiscal year: open or locked.',
  'help.field.accounting.period.body': 'A locked period rejects new entries. Unlock only for a justified correction.',
  'help.field.accounting.period.rules': 'RG-PM2|Closed period = posting blocked.',
  'help.field.accounting.period.version': 'v1.0.0',

  'help.field.accounting.lettrageAccount.title': 'Account to match',
  'help.field.accounting.lettrageAccount.n1': 'Matchable third-party account (411 / 401).',
  'help.field.accounting.lettrageAccount.body': 'Pick a matchable account. Unmatched lines then appear for pairing.',
  'help.field.accounting.lettrageAccount.version': 'v1.0.0',

  'help.field.accounting.vatMonth.title': 'VAT month',
  'help.field.accounting.vatMonth.n1': 'Tax period of the return (month + year).',
  'help.field.accounting.vatMonth.body': 'The return aggregates output and input VAT of posted entries for that month.',
  'help.field.accounting.vatMonth.version': 'v1.0.0',

  'help.field.payroll.cnss.title': 'CNSS number',
  'help.field.payroll.cnss.n1': 'Employee identifier with CNSS.',
  'help.field.payroll.cnss.body': 'Recommended for a clean DAMANCOM export.',
  'help.field.payroll.cnss.version': 'v1.0.0',

  'help.field.accounting.ocrFile.title': 'File to read',
  'help.field.accounting.ocrFile.n1': 'Scan, PDF, CSV or OFX depending on the mode.',
  'help.field.accounting.ocrFile.body': 'Invoice or DN: image or PDF. Statement: CSV, OFX, image or PDF. In Auto, the Python parser chooses the type.',
  'help.field.accounting.ocrFile.version': 'v1.0.0',

  'help.field.accounting.bankFile.title': 'Statement file',
  'help.field.accounting.bankFile.n1': 'CSV, OFX or image of the bank statement.',
  'help.field.accounting.bankFile.body': 'Import the file from the bank. Moroccan CSV columns are recognised.',
  'help.field.accounting.bankFile.version': 'v1.0.0'
};
