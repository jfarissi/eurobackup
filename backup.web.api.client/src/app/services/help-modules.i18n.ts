/** Aide in-app (N2/N3) pour modules hors ventes/achats — clés sans préfixe help. côté catalogue. */
export type HelpDict = Record<string, string>;

export const HELP_MODULES_FR: HelpDict = {
  'help.upload.tabs.title': 'Upload de documents',
  'help.upload.tabs.n1': 'Déposez un PDF fournisseur pour OCR et archivage.',
  'help.upload.tabs.body':
    'Chargez une facture, un BL ou un autre PDF.\nLe système extrait type, numéro, fournisseur et date, puis archive le fichier.\nEnsuite : Associer (Compare) → Comptabiliser dans Achats.',
  'help.upload.tabs.rules':
    'RG-FF1|Réception électronique (OCR) autorisée pour factures fournisseur.\nRG-UP1|PDF uniquement ; un fichier à la fois.\nRG-UP2|Fournisseur et type facilitent le rapprochement ultérieur.\nRG-UP3|Les documents non liés apparaissent pour association BL↔Facture.',
  'help.upload.tabs.example': 'Ex: Facture ACME FAC-8842.pdf → type Facture, fournisseur ACME, puis association au BL.',
  'help.upload.tabs.guide':
    'Déposer le PDF (ou choisir un fichier)\nRenseigner type / numéro / fournisseur / date si connus\nLancer l’upload et attendre l’OCR\nVérifier le document dans la liste\nPasser à Association si un BL ou une facture manque de lien\nComptabiliser depuis Achats → Docs parsés',
  'help.upload.tabs.version': 'v1.0.0',

  'help.upload.newDocument.title': 'Nouveau document PDF',
  'help.upload.newDocument.n1': 'Formulaire d’envoi + zone de dépôt.',
  'help.upload.newDocument.body':
    'Zone de dépôt PDF, type de document, numéro, client/fournisseur et date.\nAprès upload, le document est searchable et associable.',
  'help.upload.newDocument.guide':
    'Choisir le fichier PDF\nContrôler le type (Facture, BL…)\nCompléter fournisseur et numéro si possible\nEnvoyer',

  'help.compare.tabs.title': 'Association Facture ↔ BL',
  'help.compare.tabs.n1': 'Rapprocher factures et bons de livraison fournisseur.',
  'help.compare.tabs.body':
    'Sélectionnez une facture et un BL du même fournisseur pour comparer lignes, quantités et prix.\nValidez l’association pour lier les documents avant comptabilisation.',
  'help.compare.tabs.rules':
    'RG-AS1|Association manuelle après contrôle des écarts.\nRG-AS2|Les suggestions proposent des factures candidates pour un BL uploadé.\nRG-AS3|Écarts de prix / quantité signalés avant validation.\nRG-AS4|Comparaison possible avec le prix catalogue ERP.',
  'help.compare.tabs.example': 'Ex: BL-120 et Facture F-88 → 12 lignes OK, 1 écart prix +0,15 € signalé.',
  'help.compare.tabs.guide':
    'Ouvrir Association (éventuellement depuis un BL uploadé)\nChoisir facture et BL\nLancer la comparaison détail\nAnalyser écarts quantités/prix\nValider l’association\nExporter Excel si besoin',
  'help.compare.tabs.version': 'v1.0.0',

  'help.compare.association.title': 'Valider l’association',
  'help.compare.association.n1': 'Lie définitivement facture et BL sélectionnés.',
  'help.compare.association.body':
    'Après contrôle des écarts, le bouton lie les deux documents.\nCette liaison sert au matching et à la comptabilisation achats.',

  'help.stock.tabs.title': 'Gestion des stocks',
  'help.stock.tabs.n1': 'Niveaux, mouvements et ajustements inventaire.',
  'help.stock.tabs.body':
    'Consultez le stock par article/fournisseur et l’historique des mouvements.\nLes entrées viennent des réceptions ; les sorties des BL clients.\nL’ajustement manuel sert à l’inventaire ou à une régularisation motivée.',
  'help.stock.tabs.rules':
    'RG-RD1|Stock physique = quantité présente.\nRG-RD4|Disponible = physique − réservé.\nRG-ME1|Entrée par validation réception achat.\nRG-MS1|Sortie par validation BL client.\nRG-ME6|Régularisation manuelle = motif + traçabilité.\nRG-MS7|Stock négatif interdit par défaut.',
  'help.stock.tabs.example': 'Ex: Article VIS-M6, physique 120, réservé 20 → disponible 100.',
  'help.stock.tabs.guide':
    'Rechercher l’article\nVérifier quantité et alertes\nConsulter l’onglet mouvements\nAjuster si inventaire / casse (avec motif)\nContrôler le mouvement généré',
  'help.stock.tabs.version': 'v1.0.0',

  'help.stock.adjust.title': 'Ajustement de stock',
  'help.stock.adjust.n1': 'Correction inventaire / casse / régularisation.',
  'help.stock.adjust.body':
    'Corrige la quantité d’un article. Un mouvement de stock est généré avec le motif saisi.',
  'help.stock.adjust.rules':
    'RG-ME4|Surplus inventaire valorisé au CMP.\nRG-MS5|Manquant inventaire = charge / sortie CMP.\nRG-ME6|Motif obligatoire pour régularisation.',
  'help.stock.adjust.guide':
    'Choisir l’article\nSaisir la nouvelle quantité ou l’écart\nIndiquer le motif\nValider et contrôler le mouvement',
  'help.stock.adjust.version': 'v1.0.0',

  'help.accounting.tabs.title': 'Comptabilité',
  'help.accounting.tabs.n1': 'Journal des écritures (auto + manuelles).',
  'help.accounting.tabs.body':
    'Liste les écritures générées par factures, avoirs, paiements et achats, plus les saisies manuelles.\nFiltrez par journal, type de pièce, période.\nUne écriture manuelle doit être équilibrée (Σ débit = Σ crédit).',
  'help.accounting.tabs.rules':
    'RG-EX3|Pas d’écriture hors exercice ouvert.\nRG-EX5|Exercice clôturé = consultation seule.\nRG-PM2|Période fermée = saisie bloquée.\nRG-EC1|Écriture manuelle : totaux débit/crédit égaux.\nRG-SP1|Séparation des fonctions pour validations sensibles.',
  'help.accounting.tabs.example': 'Ex: Facture FAC-2026-0012 → écriture SalesInvoice 411 / 701 / 44571.',
  'help.accounting.tabs.guide':
    'Filtrer la période et le journal\nOuvrir une écriture pour voir les lignes\nCréer une écriture manuelle si besoin\nÉquilibrer débit / crédit\nEnregistrer',
  'help.accounting.tabs.version': 'v1.0.0',

  'help.accounting.newEntry.title': 'Écriture comptable manuelle',
  'help.accounting.newEntry.n1': 'Saisie libre journal + lignes D/C.',
  'help.accounting.newEntry.body':
    'Journal, date, libellé et lignes débit/crédit.\nLes totaux doivent être équilibrés avant enregistrement.',
  'help.accounting.newEntry.rules':
    'RG-EC1|Σ débit = Σ crédit.\nRG-EX3|Date dans l’exercice / période ouverte.',
  'help.accounting.newEntry.guide':
    'Choisir le journal et la date\nSaisir le libellé\nAjouter au moins 2 lignes (D et C)\nVérifier l’équilibre\nEnregistrer',
  'help.accounting.newEntry.version': 'v1.0.0',

  'help.cash.tabs.title': 'Caisse magasin',
  'help.cash.tabs.n1': 'Sessions d’espèces : ouverture, opérations, clôture.',
  'help.cash.tabs.body':
    'Une session = une journée (ou vacation) de caisse.\nOuvrez avec le fond, enregistrez apports/retraits, clôturez avec le comptage réel.\nL’écart (attendu vs réel) est historisé.',
  'help.cash.tabs.rules':
    'RG-CX1|Une seule session ouverte à la fois par société.\nRG-CX2|Fond de caisse saisi à l’ouverture.\nRG-CX3|Opérations uniquement sur session ouverte.\nRG-CX4|Clôture : solde réel compté + écart enregistré.\nRG-ER4|Encaissements CB suivent le circuit bancaire (hors tiroir).',
  'help.cash.tabs.example': 'Ex: Ouverture 50 €, ventes espèces 320 €, retrait 100 € → attendu 270 € ; compté 268 € → écart −2 €.',
  'help.cash.tabs.guide':
    'Ouvrir la session (fond de caisse)\nEnregistrer opérations si besoin\nEn fin de vacation : compter le tiroir\nFermer et noter l’écart\nConsulter l’historique',
  'help.cash.tabs.version': 'v1.0.0',

  'help.cash.open.title': 'Ouvrir la caisse',
  'help.cash.open.n1': 'Démarre la session avec le fond initial.',
  'help.cash.open.body': 'Saisissez le fond de caisse. Une seule session ouverte à la fois.',
  'help.cash.open.rules': 'RG-CX1|Une session ouverte max.\nRG-CX2|Fond initial obligatoire.',
  'help.cash.open.guide': 'Vérifier qu’aucune session n’est ouverte\nSaisir le fond\nConfirmer l’ouverture',
  'help.cash.open.version': 'v1.0.0',

  'help.cash.close.title': 'Fermer la caisse',
  'help.cash.close.n1': 'Clôture avec comptage réel.',
  'help.cash.close.body': 'Saisissez le solde compté. L’écart vs solde attendu est enregistré.',
  'help.cash.close.rules': 'RG-CX4|Comptage réel + écart historisé.',
  'help.cash.close.guide': 'Compter le tiroir\nSaisir le montant réel\nContrôler l’écart affiché\nConfirmer la clôture',
  'help.cash.close.version': 'v1.0.0',

  'help.cash.newOp.title': 'Opération de caisse',
  'help.cash.newOp.n1': 'Entrée / sortie d’espèces sur la session.',
  'help.cash.newOp.body': 'Apport, retrait ou régularisation d’espèces pendant la session ouverte.',
  'help.cash.newOp.rules': 'RG-CX3|Uniquement si session ouverte.',
  'help.cash.newOp.guide': 'Choisir le type\nSaisir montant et motif\nEnregistrer',
  'help.cash.newOp.version': 'v1.0.0',

  'help.erpProducts.tabs.title': 'Produits ERP',
  'help.erpProducts.tabs.n1': 'Catalogue articles synchronisé avec l’ERP source.',
  'help.erpProducts.tabs.body':
    'Liste des produits (référence, EAN, prix, marque, catégories).\nEnrichissez depuis l’ERP et suivez les changements via l’écran Changements.',
  'help.erpProducts.tabs.rules':
    'RG-PR1|La référence produit est unique par société.\nRG-PR2|L’enrichissement met à jour prix / libellés depuis l’ERP sans écraser les liens locaux non synchronisés.\nRG-PR3|Création possible depuis une ligne document (OCR) si l’article est inconnu.',
  'help.erpProducts.tabs.example': 'Ex: Sync catalogue → 1 240 produits mis à jour, 12 créés.',
  'help.erpProducts.tabs.guide':
    'Filtrer / rechercher un article\nOuvrir la fiche détail\nLancer enrichissement ERP si besoin\nConsulter Changements pour l’historique',
  'help.erpProducts.tabs.version': 'v1.0.0',

  'help.erpChanges.tabs.title': 'Changements produits ERP',
  'help.erpChanges.tabs.n1': 'Journal des créations / mises à jour prix & stock.',
  'help.erpChanges.tabs.body':
    'Histor des événements de sync (Created, Updated, Price, Stock).\nImport Excel et sync ERP alimentent ce journal pour audit.',
  'help.erpChanges.tabs.rules':
    'RG-CH1|Chaque sync produit une trace (quoi, quand, avant/après).\nRG-CH2|Import Excel peut être suivi d’un enrichissement.\nRG-CH3|Utile pour expliquer un écart de prix Facture↔ERP.',
  'help.erpChanges.tabs.example': 'Ex: Prix VIS-M6 0,12 → 0,14 € le 03/08 suite sync.',
  'help.erpChanges.tabs.guide':
    'Filtrer par type de changement\nLire l’écart avant/après\nRelancer sync ou import si catalogue obsolète',
  'help.erpChanges.tabs.version': 'v1.0.0',

  'help.admin.tabs.title': 'Administration',
  'help.admin.tabs.n1': 'Tenants, sociétés, rôles, utilisateurs et CMS d’aide.',
  'help.admin.tabs.body':
    'Paramétrage multi-société : organisation (tenant), sociétés, rôles/permissions, comptes utilisateurs.\nL’onglet Aide publie des contenus d’aide (CMS) qui surchargent les textes i18n.',
  'help.admin.tabs.rules':
    'RG-AU1|Authentification centralisée / politique de mot de passe.\nRG-AU2|Mot de passe fort (longueur et complexité).\nRG-HA1|Droits par fonction × objet × périmètre société.\nRG-HA2|Matrice CRUDV (créer, lire, modifier, supprimer, valider).',
  'help.admin.tabs.example': 'Ex: Rôle Comptable → Accounting.*, Invoice.Read ; pas Cash.Manage.',
  'help.admin.tabs.guide':
    'Créer / activer le tenant\nCréer les sociétés\nDéfinir les rôles et permissions\nCréer les utilisateurs et affecter sociétés\nPublier l’aide CMS si besoin',
  'help.admin.tabs.version': 'v1.0.0',

  'help.admin.tenant.title': 'Tenant',
  'help.admin.tenant.n1': 'Organisation regroupant sociétés et utilisateurs.',
  'help.admin.tenant.body': 'Gérez le nom et l’activation du tenant.',
  'help.admin.tenant.version': 'v1.0.0',

  'help.admin.company.title': 'Société',
  'help.admin.company.n1': 'Entité opérationnelle (documents, stock, caisse).',
  'help.admin.company.body':
    'Société rattachée à un tenant : nom, langue, devise.\nLes documents et stocks sont liés à la société active.',
  'help.admin.company.version': 'v1.0.0',

  'help.admin.roles.title': 'Rôles et permissions',
  'help.admin.roles.n1': 'Matrice CRUDV par domaine métier.',
  'help.admin.roles.body':
    'Un rôle regroupe des permissions (ex. Invoice.Create).\nAssignez les rôles aux utilisateurs selon le principe du moindre privilège.',
  'help.admin.roles.rules': 'RG-HA1|Granularité fonction × objet.\nRG-HA2|CRUDV par objet métier.',
  'help.admin.roles.version': 'v1.0.0',

  'help.admin.user.title': 'Utilisateur',
  'help.admin.user.n1': 'Compte de connexion.',
  'help.admin.user.body': 'Identifiant, rôles et rattachement aux sociétés.',
  'help.admin.user.version': 'v1.0.0',

  'help.admin.resetPassword.title': 'Réinitialiser le mot de passe',
  'help.admin.resetPassword.n1': 'Nouveau mot de passe administrateur.',
  'help.admin.resetPassword.body': 'Définit un nouveau mot de passe pour l’utilisateur sélectionné.',
  'help.admin.resetPassword.rules': 'RG-AU2|Respecter la politique de complexité.',
  'help.admin.resetPassword.version': 'v1.0.0',

  'help.admin.assign.title': 'Affecter des sociétés',
  'help.admin.assign.n1': 'Périmètre multi-société de l’utilisateur.',
  'help.admin.assign.body': 'Choisissez les sociétés accessibles à l’utilisateur.',
  'help.admin.assign.version': 'v1.0.0',

  'help.admin.helpCms.title': 'CMS Aide',
  'help.admin.helpCms.n1': 'Publier des textes d’aide métier.',
  'help.admin.helpCms.body':
    'Créez / publiez des contenus qui remplacent l’i18n pour une clé d’aide et une langue.\nNécessite la permission Help.Manage.',
  'help.admin.helpCms.guide':
    'Choisir la clé (ex. sales.invoice)\nRédiger titre, corps, règles, guide\nPublier\nVérifier dans le centre d’aide (F1)',
  'help.admin.helpCms.version': 'v1.0.0',

  'help.numbering.tabs.title': 'Numérotation des documents',
  'help.numbering.tabs.n1': 'Séquences FAC, BL, CMD… par société.',
  'help.numbering.tabs.body':
    'Chaque type de document a un format (préfixe, année, compteur).\nLes numéros définitifs sont attribués à la validation.',
  'help.numbering.tabs.rules':
    'RG-T1|Numérotation chronologique sans trou après validation.\nRG-NUM1|Format paramétrable via jetons {Prefix}, {Year}, {Number:D4}.\nRG-NUM2|Compteur par société et type de document.',
  'help.numbering.tabs.example': 'Ex: FAC-{Year}-{Number:D4} → FAC-2026-0001.',
  'help.numbering.tabs.guide':
    'Initialiser les séquences par défaut si besoin\nAjuster le format\nPrévisualiser\nEnregistrer',
  'help.numbering.tabs.version': 'v1.0.0',

  'help.createProduct.title': 'Créer un produit ERP',
  'help.createProduct.n1': 'Article depuis une ligne de document OCR.',
  'help.createProduct.body':
    'Crée un article (référence, EAN, prix, marque, catégories) quand le parsing ne trouve pas le produit.',
  'help.createProduct.rules': 'RG-PR1|Référence unique.\nRG-PR3|Création depuis ligne document autorisée.',
  'help.createProduct.guide':
    'Contrôler référence et EAN\nCompléter prix et libellé\nChoisir marque / catégories\nCréer puis relancer le matching',
  'help.createProduct.version': 'v1.0.0',

  'help.field.upload.supplier.title': 'Fournisseur',
  'help.field.upload.supplier.n1': 'Tiers du PDF uploadé.',
  'help.field.upload.supplier.body': 'Facilite le filtrage des documents non liés et l’association.',

  'help.field.cash.openingBalance.title': 'Fond de caisse',
  'help.field.cash.openingBalance.n1': 'Espèces présentes à l’ouverture.',
  'help.field.cash.openingBalance.body': 'Montant compté dans le tiroir au démarrage de la session.',

  'help.field.stock.quantity.title': 'Quantité stock',
  'help.field.stock.quantity.n1': 'Nouvelle quantité après ajustement.',
  'help.field.stock.quantity.body': 'Doit refléter le physique constaté. Un mouvement d’écart est généré.',

  'help.field.accounting.account.title': 'Compte comptable',
  'help.field.accounting.account.n1': 'N° de compte de la ligne.',
  'help.field.accounting.account.body': 'Utilisez le plan de comptes (ex. 411 clients, 401 fournisseurs, 512 banque).',

  'help.field.numbering.format.title': 'Format de numéro',
  'help.field.numbering.format.n1': 'Motif avec jetons.',
  'help.field.numbering.format.body': 'Jetons : {Prefix}, {Year}, {Number:D4}… Prévisualisez avant d’enregistrer.'
};

export const HELP_MODULES_NL: HelpDict = {
  'help.upload.tabs.title': 'Documenten uploaden',
  'help.upload.tabs.n1': 'PDF van leverancier voor OCR en archivering.',
  'help.upload.tabs.body':
    'Upload een factuur, LB of andere PDF.\nHet systeem haalt type, nummer, leverancier en datum op.\nDaarna: Associatie (Compare) → Boeken in Aankopen.',
  'help.upload.tabs.rules':
    'RG-FF1|Elektronische ontvangst (OCR) toegestaan.\nRG-UP1|Alleen PDF; één bestand tegelijk.\nRG-UP2|Leverancier en type helpen later matchen.\nRG-UP3|Niet-gekoppelde documenten verschijnen voor LB↔Factuur.',
  'help.upload.tabs.example': 'Vb: Factuur ACME FAC-8842.pdf → type Factuur, leverancier ACME, daarna koppelen aan LB.',
  'help.upload.tabs.guide':
    'PDF neerzetten\nType / nummer / leverancier / datum invullen\nUploaden en OCR afwachten\nDocument controleren\nNaar Associatie als koppeling ontbreekt\nBoeken via Aankopen → Geparsede docs',
  'help.upload.tabs.version': 'v1.0.0',

  'help.upload.newDocument.title': 'Nieuw PDF-document',
  'help.upload.newDocument.n1': 'Uploadformulier + dropzone.',
  'help.upload.newDocument.body': 'PDF-zone, type, nummer, klant/leverancier en datum. Na upload doorzoekbaar en koppelbaar.',
  'help.upload.newDocument.guide': 'PDF kiezen\nType controleren\nLeverancier/nummer invullen\nVerzenden',

  'help.compare.tabs.title': 'Associatie Factuur ↔ LB',
  'help.compare.tabs.n1': 'Facturen en leveringsbonnen van leveranciers matchen.',
  'help.compare.tabs.body':
    'Selecteer een factuur en een LB van dezelfde leverancier om regels, hoeveelheden en prijzen te vergelijken.\nBevestig de koppeling vóór boeking.',
  'help.compare.tabs.rules':
    'RG-AS1|Manuele associatie na controle van afwijkingen.\nRG-AS2|Suggesties voor een geüploade LB.\nRG-AS3|Prijs-/hoeveelheidsverschillen vóór validatie.\nRG-AS4|Vergelijking met ERP-catalogusprijs mogelijk.',
  'help.compare.tabs.example': 'Vb: LB-120 en Factuur F-88 → 12 regels OK, 1 prijsverschil +0,15 €.',
  'help.compare.tabs.guide':
    'Associatie openen\nFactuur en LB kiezen\nDetailvergelijking starten\nAfwijkingen analyseren\nAssociatie bevestigen\nEventueel Excel exporteren',
  'help.compare.tabs.version': 'v1.0.0',

  'help.compare.association.title': 'Associatie bevestigen',
  'help.compare.association.n1': 'Koppelt factuur en LB definitief.',
  'help.compare.association.body': 'Na controle van afwijkingen worden de documenten gekoppeld voor matching en aankoopboeking.',

  'help.stock.tabs.title': 'Voorraadbeheer',
  'help.stock.tabs.n1': 'Niveaus, bewegingen en inventarisaanpassingen.',
  'help.stock.tabs.body':
    'Bekijk voorraad per artikel/leverancier en bewegingshistoriek.\nIngangen via ontvangsten; uitgangen via klant-LB.\nManuele aanpassing voor inventaris of gemotiveerde regularisatie.',
  'help.stock.tabs.rules':
    'RG-RD1|Fysieke voorraad = aanwezige hoeveelheid.\nRG-RD4|Beschikbaar = fysiek − gereserveerd.\nRG-ME1|Ingang bij validatie aankoopontvangst.\nRG-MS1|Uitgang bij validatie klant-LB.\nRG-ME6|Manuele regularisatie = reden + traceerbaarheid.\nRG-MS7|Negatieve voorraad standaard verboden.',
  'help.stock.tabs.example': 'Vb: Artikel VIS-M6, fysiek 120, gereserveerd 20 → beschikbaar 100.',
  'help.stock.tabs.guide':
    'Artikel zoeken\nHoeveelheid controleren\nTab bewegingen raadplegen\nAanpassen bij inventaris/schade\nBeweging controleren',
  'help.stock.tabs.version': 'v1.0.0',

  'help.stock.adjust.title': 'Voorraadaanpassing',
  'help.stock.adjust.n1': 'Correctie inventaris / schade / regularisatie.',
  'help.stock.adjust.body': 'Corrigeert de hoeveelheid. Er wordt een voorraadbeweging met reden aangemaakt.',
  'help.stock.adjust.rules':
    'RG-ME4|Inventaris-overschot gewaardeerd tegen GWP.\nRG-MS5|Inventaris-tekort = uitgang GWP.\nRG-ME6|Reden verplicht.',
  'help.stock.adjust.guide': 'Artikel kiezen\nNieuwe hoeveelheid of verschil invoeren\nReden opgeven\nBevestigen',
  'help.stock.adjust.version': 'v1.0.0',

  'help.accounting.tabs.title': 'Boekhouding',
  'help.accounting.tabs.n1': 'Journaal (auto + manueel).',
  'help.accounting.tabs.body':
    'Toont boekingen uit facturen, creditnota’s, betalingen en aankopen, plus manuele boekingen.\nFilter op journaal, stuktype, periode.\nManuele boeking moet in evenwicht zijn (Σ debet = Σ credit).',
  'help.accounting.tabs.rules':
    'RG-EX3|Geen boeking buiten open boekjaar.\nRG-EX5|Gesloten boekjaar = alleen raadpleging.\nRG-PM2|Gesloten periode = geen invoer.\nRG-EC1|Manuele boeking: debet = credit.\nRG-SP1|Scheiding van functies bij gevoelige validaties.',
  'help.accounting.tabs.example': 'Vb: Factuur FAC-2026-0012 → SalesInvoice 411 / 701 / 44571.',
  'help.accounting.tabs.guide':
    'Periode en journaal filteren\nBoeking openen\nEventueel manuele boeking maken\nEvenwicht controleren\nOpslaan',
  'help.accounting.tabs.version': 'v1.0.0',

  'help.accounting.newEntry.title': 'Handmatige boeking',
  'help.accounting.newEntry.n1': 'Vrije journaalinvoer + D/C-regels.',
  'help.accounting.newEntry.body': 'Journaal, datum, omschrijving en debet-/creditregels. Totalen moeten in evenwicht zijn.',
  'help.accounting.newEntry.rules': 'RG-EC1|Σ debet = Σ credit.\nRG-EX3|Datum in open boekjaar/periode.',
  'help.accounting.newEntry.guide': 'Journaal en datum kiezen\nOmschrijving\nMinstens 2 regels\nEvenwicht controleren\nOpslaan',
  'help.accounting.newEntry.version': 'v1.0.0',

  'help.cash.tabs.title': 'Winkelkassa',
  'help.cash.tabs.n1': 'Kassasessies: openen, bewerkingen, sluiten.',
  'help.cash.tabs.body':
    'Eén sessie = één dag/shift.\nOpen met openingskas, registreer in/uit, sluit met telling.\nVerschil (verwacht vs reëel) wordt bewaard.',
  'help.cash.tabs.rules':
    'RG-CX1|Maximaal één open sessie per bedrijf.\nRG-CX2|Openingskas bij opening.\nRG-CX3|Bewerkingen alleen op open sessie.\nRG-CX4|Sluiting: geteld saldo + verschil.\nRG-ER4|Kaartbetalingen via bankcircuit.',
  'help.cash.tabs.example': 'Vb: Opening 50 €, cashverkopen 320 €, opname 100 € → verwacht 270 €; geteld 268 € → verschil −2 €.',
  'help.cash.tabs.guide':
    'Sessie openen\nBewerkingen registreren\nTellen\nSluiten en verschil noteren\nHistoriek raadplegen',
  'help.cash.tabs.version': 'v1.0.0',

  'help.cash.open.title': 'Kassa openen',
  'help.cash.open.n1': 'Start sessie met openingskas.',
  'help.cash.open.body': 'Geef de openingskas in. Slechts één open sessie tegelijk.',
  'help.cash.open.rules': 'RG-CX1|Max. één open sessie.\nRG-CX2|Openingskas verplicht.',
  'help.cash.open.guide': 'Controleren dat geen sessie open is\nOpeningskas invoeren\nBevestigen',
  'help.cash.open.version': 'v1.0.0',

  'help.cash.close.title': 'Kassa sluiten',
  'help.cash.close.n1': 'Sluiting met telling.',
  'help.cash.close.body': 'Geef het getelde saldo in. Het verschil vs verwacht wordt bewaard.',
  'help.cash.close.rules': 'RG-CX4|Telling + verschil historiseren.',
  'help.cash.close.guide': 'Teller laden\nBedrag invoeren\nVerschil controleren\nSluiting bevestigen',
  'help.cash.close.version': 'v1.0.0',

  'help.cash.newOp.title': 'Kassaoperatie',
  'help.cash.newOp.n1': 'Contante in-/uitgaande beweging.',
  'help.cash.newOp.body': 'Storting, opname of regularisatie op de open sessie.',
  'help.cash.newOp.rules': 'RG-CX3|Alleen bij open sessie.',
  'help.cash.newOp.guide': 'Type kiezen\nBedrag en reden\nOpslaan',
  'help.cash.newOp.version': 'v1.0.0',

  'help.erpProducts.tabs.title': 'ERP-producten',
  'help.erpProducts.tabs.n1': 'Artikelcatalogus gesynchroniseerd met bron-ERP.',
  'help.erpProducts.tabs.body':
    'Productlijst (referentie, EAN, prijs, merk, categorieën).\nVerrijk vanuit ERP en volg wijzigingen via Wijzigingen.',
  'help.erpProducts.tabs.rules':
    'RG-PR1|Unieke productreferentie per bedrijf.\nRG-PR2|Verrijking werkt prijzen/labels bij.\nRG-PR3|Aanmaken vanuit documentregel (OCR) mogelijk.',
  'help.erpProducts.tabs.example': 'Vb: Catalogussync → 1 240 bijgewerkt, 12 aangemaakt.',
  'help.erpProducts.tabs.guide':
    'Artikel zoeken\nDetail openen\nERP-verrijking starten\nWijzigingen raadplegen',
  'help.erpProducts.tabs.version': 'v1.0.0',

  'help.erpChanges.tabs.title': 'ERP-productwijzigingen',
  'help.erpChanges.tabs.n1': 'Log van creaties / prijs- & voorraadupdates.',
  'help.erpChanges.tabs.body':
    'Overzicht van sync-events (Created, Updated, Price, Stock).\nExcel-import en ERP-sync vullen dit auditlog.',
  'help.erpChanges.tabs.rules':
    'RG-CH1|Elke sync laat een spoor na.\nRG-CH2|Excel-import kan gevolgd worden door verrijking.\nRG-CH3|Nuttig bij prijsverschil Factuur↔ERP.',
  'help.erpChanges.tabs.example': 'Vb: Prijs VIS-M6 0,12 → 0,14 € op 03/08 na sync.',
  'help.erpChanges.tabs.guide':
    'Filteren op type\nVoor/na lezen\nSync of import herstarten indien nodig',
  'help.erpChanges.tabs.version': 'v1.0.0',

  'help.admin.tabs.title': 'Administratie',
  'help.admin.tabs.n1': 'Tenants, bedrijven, rollen, gebruikers en help-CMS.',
  'help.admin.tabs.body':
    'Multi-bedrijf: organisatie (tenant), bedrijven, rollen/rechten, gebruikers.\nTab Help publiceert CMS-inhoud boven i18n.',
  'help.admin.tabs.rules':
    'RG-AU1|Centrale authenticatie / wachtwoordbeleid.\nRG-AU2|Sterk wachtwoord.\nRG-HA1|Rechten per functie × object × bedrijf.\nRG-HA2|CRUDV-matrix.',
  'help.admin.tabs.example': 'Vb: Rol Boekhouder → Accounting.*, Invoice.Read ; geen Cash.Manage.',
  'help.admin.tabs.guide':
    'Tenant aanmaken\nBedrijven aanmaken\nRollen/rechten\nGebruikers + bedrijven\nHelp-CMS publiceren',
  'help.admin.tabs.version': 'v1.0.0',

  'help.admin.tenant.title': 'Tenant',
  'help.admin.tenant.n1': 'Organisatie met bedrijven en gebruikers.',
  'help.admin.tenant.body': 'Beheer naam en activering van de tenant.',
  'help.admin.tenant.version': 'v1.0.0',

  'help.admin.company.title': 'Bedrijf',
  'help.admin.company.n1': 'Operationele entiteit (documenten, voorraad, kassa).',
  'help.admin.company.body': 'Bedrijf gekoppeld aan een tenant: naam, taal, valuta.',
  'help.admin.company.version': 'v1.0.0',

  'help.admin.roles.title': 'Rollen en rechten',
  'help.admin.roles.n1': 'CRUDV-matrix per domein.',
  'help.admin.roles.body': 'Een rol bundelt rechten (bv. Invoice.Create). Wijs toe volgens least privilege.',
  'help.admin.roles.rules': 'RG-HA1|Granulariteit functie × object.\nRG-HA2|CRUDV per object.',
  'help.admin.roles.version': 'v1.0.0',

  'help.admin.user.title': 'Gebruiker',
  'help.admin.user.n1': 'Loginaccount.',
  'help.admin.user.body': 'Gebruikersnaam, rollen en toegang tot bedrijven.',
  'help.admin.user.version': 'v1.0.0',

  'help.admin.resetPassword.title': 'Wachtwoord resetten',
  'help.admin.resetPassword.n1': 'Nieuw admin-wachtwoord.',
  'help.admin.resetPassword.body': 'Stelt een nieuw wachtwoord in voor de geselecteerde gebruiker.',
  'help.admin.resetPassword.rules': 'RG-AU2|Complexiteitsbeleid respecteren.',
  'help.admin.resetPassword.version': 'v1.0.0',

  'help.admin.assign.title': 'Bedrijven toewijzen',
  'help.admin.assign.n1': 'Multi-bedrijfbereik van de gebruiker.',
  'help.admin.assign.body': 'Kies de bedrijven waartoe de gebruiker toegang heeft.',
  'help.admin.assign.version': 'v1.0.0',

  'help.admin.helpCms.title': 'Help-CMS',
  'help.admin.helpCms.n1': 'Helpteksten publiceren.',
  'help.admin.helpCms.body': 'Publiceer inhoud die i18n vervangt voor een help-sleutel en taal. Vereist Help.Manage.',
  'help.admin.helpCms.guide': 'Sleutel kiezen\nTitel/body/regels/gids schrijven\nPubliceren\nControleren in helpcentrum (F1)',
  'help.admin.helpCms.version': 'v1.0.0',

  'help.numbering.tabs.title': 'Documentnummering',
  'help.numbering.tabs.n1': 'Reeksen FAC, LB, CMD… per bedrijf.',
  'help.numbering.tabs.body':
    'Elk documenttype heeft een formaat (prefix, jaar, teller).\nDefinitieve nummers bij validatie.',
  'help.numbering.tabs.rules':
    'RG-T1|Chronologische nummering zonder gaten na validatie.\nRG-NUM1|Formaat via tokens {Prefix}, {Year}, {Number:D4}.\nRG-NUM2|Teller per bedrijf en type.',
  'help.numbering.tabs.example': 'Vb: FAC-{Year}-{Number:D4} → FAC-2026-0001.',
  'help.numbering.tabs.guide':
    'Standaardreeksen initialiseren\nFormaat aanpassen\nVoorbeeld bekijken\nOpslaan',
  'help.numbering.tabs.version': 'v1.0.0',

  'help.createProduct.title': 'ERP-product aanmaken',
  'help.createProduct.n1': 'Artikel vanuit OCR-regel.',
  'help.createProduct.body':
    'Maakt een artikel (referentie, EAN, prijs, merk, categorieën) als parsing het product niet vindt.',
  'help.createProduct.rules': 'RG-PR1|Unieke referentie.\nRG-PR3|Aanmaken vanuit documentregel toegestaan.',
  'help.createProduct.guide':
    'Referentie/EAN controleren\nPrijs en label\nMerk/categorieën\nAanmaken en matching herstarten',
  'help.createProduct.version': 'v1.0.0',

  'help.field.upload.supplier.title': 'Leverancier',
  'help.field.upload.supplier.n1': 'Partij van de geüploade PDF.',
  'help.field.upload.supplier.body': 'Helpt bij filteren van niet-gekoppelde documenten en associatie.',

  'help.field.cash.openingBalance.title': 'Openingskas',
  'help.field.cash.openingBalance.n1': 'Contant bij opening.',
  'help.field.cash.openingBalance.body': 'Geteld bedrag in de lade bij start van de sessie.',

  'help.field.stock.quantity.title': 'Voorraadhoeveelheid',
  'help.field.stock.quantity.n1': 'Nieuwe hoeveelheid na aanpassing.',
  'help.field.stock.quantity.body': 'Moet de fysieke telling weerspiegelen. Er wordt een verschilbeweging gemaakt.',

  'help.field.accounting.account.title': 'Grootboekrekening',
  'help.field.accounting.account.n1': 'Rekeningnummer van de regel.',
  'help.field.accounting.account.body': 'Gebruik het rekeningenstelsel (bv. 411 klanten, 401 leveranciers, 512 bank).',

  'help.field.numbering.format.title': 'Nummerformaat',
  'help.field.numbering.format.n1': 'Patroon met tokens.',
  'help.field.numbering.format.body': 'Tokens: {Prefix}, {Year}, {Number:D4}… Bekijk voorbeeld vóór opslaan.'
};

export const HELP_MODULES_EN: HelpDict = {
  'help.upload.tabs.title': 'Document upload',
  'help.upload.tabs.n1': 'Drop a supplier PDF for OCR and archiving.',
  'help.upload.tabs.body':
    'Upload an invoice, DN or other PDF.\nThe system extracts type, number, supplier and date, then archives the file.\nNext: Associate (Compare) → Post in Purchases.',
  'help.upload.tabs.rules':
    'RG-FF1|Electronic receipt (OCR) allowed for supplier invoices.\nRG-UP1|PDF only; one file at a time.\nRG-UP2|Supplier and type help later matching.\nRG-UP3|Unlinked documents appear for DN↔Invoice association.',
  'help.upload.tabs.example': 'E.g. Invoice ACME FAC-8842.pdf → type Invoice, supplier ACME, then link to DN.',
  'help.upload.tabs.guide':
    'Drop the PDF\nFill type / number / supplier / date if known\nUpload and wait for OCR\nCheck the document list\nGo to Association if a link is missing\nPost from Purchases → Parsed docs',
  'help.upload.tabs.version': 'v1.0.0',

  'help.upload.newDocument.title': 'New PDF document',
  'help.upload.newDocument.n1': 'Upload form + drop zone.',
  'help.upload.newDocument.body':
    'PDF drop zone, document type, number, customer/supplier and date.\nAfter upload the document is searchable and linkable.',
  'help.upload.newDocument.guide': 'Choose the PDF\nCheck the type\nFill supplier/number if possible\nSubmit',

  'help.compare.tabs.title': 'Invoice ↔ DN association',
  'help.compare.tabs.n1': 'Match supplier invoices and delivery notes.',
  'help.compare.tabs.body':
    'Select an invoice and a DN from the same supplier to compare lines, quantities and prices.\nValidate the association before posting purchases.',
  'help.compare.tabs.rules':
    'RG-AS1|Manual association after variance review.\nRG-AS2|Suggestions for an uploaded DN.\nRG-AS3|Price/qty gaps flagged before validation.\nRG-AS4|Compare with ERP catalog price.',
  'help.compare.tabs.example': 'E.g. DN-120 and Invoice F-88 → 12 lines OK, 1 price gap +€0.15.',
  'help.compare.tabs.guide':
    'Open Association\nPick invoice and DN\nRun detailed comparison\nReview gaps\nValidate association\nExport Excel if needed',
  'help.compare.tabs.version': 'v1.0.0',

  'help.compare.association.title': 'Validate association',
  'help.compare.association.n1': 'Permanently links selected invoice and DN.',
  'help.compare.association.body':
    'After reviewing gaps, the button links both documents for matching and purchase posting.',

  'help.stock.tabs.title': 'Stock management',
  'help.stock.tabs.n1': 'Levels, movements and inventory adjustments.',
  'help.stock.tabs.body':
    'Browse stock by item/supplier and movement history.\nInbound from receipts; outbound from sales DNs.\nManual adjustment for inventory or justified regularization.',
  'help.stock.tabs.rules':
    'RG-RD1|Physical stock = on-hand qty.\nRG-RD4|Available = physical − reserved.\nRG-ME1|Inbound on purchase receipt validation.\nRG-MS1|Outbound on sales DN validation.\nRG-ME6|Manual regularization = reason + audit.\nRG-MS7|Negative stock forbidden by default.',
  'help.stock.tabs.example': 'E.g. Item VIS-M6, physical 120, reserved 20 → available 100.',
  'help.stock.tabs.guide':
    'Search the item\nCheck quantity and alerts\nOpen movements tab\nAdjust for inventory/damage\nVerify the movement',
  'help.stock.tabs.version': 'v1.0.0',

  'help.stock.adjust.title': 'Stock adjustment',
  'help.stock.adjust.n1': 'Inventory / damage / regularization correction.',
  'help.stock.adjust.body': 'Corrects item quantity. A stock movement is created with the reason entered.',
  'help.stock.adjust.rules':
    'RG-ME4|Inventory surplus valued at WAC.\nRG-MS5|Inventory shortage = WAC outbound.\nRG-ME6|Reason required.',
  'help.stock.adjust.guide': 'Pick the item\nEnter new qty or variance\nEnter reason\nConfirm',
  'help.stock.adjust.version': 'v1.0.0',

  'help.accounting.tabs.title': 'Accounting',
  'help.accounting.tabs.n1': 'Journal entries (auto + manual).',
  'help.accounting.tabs.body':
    'Lists entries from invoices, credit notes, payments and purchases, plus manual entries.\nFilter by journal, reference type, period.\nA manual entry must balance (Σ debit = Σ credit).',
  'help.accounting.tabs.rules':
    'RG-EX3|No posting outside the open fiscal year.\nRG-EX5|Closed year = read-only.\nRG-PM2|Closed period = no entry.\nRG-EC1|Manual entry: debit = credit.\nRG-SP1|Segregation of duties for sensitive approvals.',
  'help.accounting.tabs.example': 'E.g. Invoice FAC-2026-0012 → SalesInvoice 411 / 701 / 44571.',
  'help.accounting.tabs.guide':
    'Filter period and journal\nOpen an entry to see lines\nCreate a manual entry if needed\nBalance debit/credit\nSave',
  'help.accounting.tabs.version': 'v1.0.0',

  'help.accounting.newEntry.title': 'Manual journal entry',
  'help.accounting.newEntry.n1': 'Free journal + D/C lines.',
  'help.accounting.newEntry.body':
    'Journal, date, description and debit/credit lines.\nTotals must balance before save.',
  'help.accounting.newEntry.rules': 'RG-EC1|Σ debit = Σ credit.\nRG-EX3|Date in open year/period.',
  'help.accounting.newEntry.guide': 'Choose journal and date\nEnter description\nAdd at least 2 lines\nCheck balance\nSave',
  'help.accounting.newEntry.version': 'v1.0.0',

  'help.cash.tabs.title': 'Store cash register',
  'help.cash.tabs.n1': 'Cash sessions: open, operations, close.',
  'help.cash.tabs.body':
    'One session = one day/shift.\nOpen with float, record cash in/out, close with counted cash.\nVariance (expected vs actual) is stored.',
  'help.cash.tabs.rules':
    'RG-CX1|Only one open session per company.\nRG-CX2|Opening float required.\nRG-CX3|Operations only on open session.\nRG-CX4|Close: counted balance + variance.\nRG-ER4|Card settlements follow the bank flow.',
  'help.cash.tabs.example': 'E.g. Open €50, cash sales €320, withdrawal €100 → expected €270; counted €268 → variance −€2.',
  'help.cash.tabs.guide':
    'Open the session\nRecord operations if needed\nCount the drawer\nClose and note variance\nReview history',
  'help.cash.tabs.version': 'v1.0.0',

  'help.cash.open.title': 'Open cash register',
  'help.cash.open.n1': 'Starts the session with opening float.',
  'help.cash.open.body': 'Enter the opening float. Only one open session at a time.',
  'help.cash.open.rules': 'RG-CX1|Max one open session.\nRG-CX2|Opening float required.',
  'help.cash.open.guide': 'Ensure no session is open\nEnter float\nConfirm',
  'help.cash.open.version': 'v1.0.0',

  'help.cash.close.title': 'Close cash register',
  'help.cash.close.n1': 'Close with counted cash.',
  'help.cash.close.body': 'Enter the counted balance. Variance vs expected is stored.',
  'help.cash.close.rules': 'RG-CX4|Count + variance historized.',
  'help.cash.close.guide': 'Count the drawer\nEnter amount\nCheck variance\nConfirm close',
  'help.cash.close.version': 'v1.0.0',

  'help.cash.newOp.title': 'Cash operation',
  'help.cash.newOp.n1': 'Cash in/out on the session.',
  'help.cash.newOp.body': 'Deposit, withdrawal or cash regularization on the open session.',
  'help.cash.newOp.rules': 'RG-CX3|Only when a session is open.',
  'help.cash.newOp.guide': 'Pick type\nEnter amount and reason\nSave',
  'help.cash.newOp.version': 'v1.0.0',

  'help.erpProducts.tabs.title': 'ERP products',
  'help.erpProducts.tabs.n1': 'Item catalog synced from source ERP.',
  'help.erpProducts.tabs.body':
    'Product list (reference, EAN, price, brand, categories).\nEnrich from ERP and track changes on the Changes screen.',
  'help.erpProducts.tabs.rules':
    'RG-PR1|Product reference unique per company.\nRG-PR2|Enrichment updates prices/labels from ERP.\nRG-PR3|Create from a document line (OCR) when unknown.',
  'help.erpProducts.tabs.example': 'E.g. Catalog sync → 1,240 updated, 12 created.',
  'help.erpProducts.tabs.guide':
    'Filter/search an item\nOpen detail\nRun ERP enrichment if needed\nOpen Changes for history',
  'help.erpProducts.tabs.version': 'v1.0.0',

  'help.erpChanges.tabs.title': 'ERP product changes',
  'help.erpChanges.tabs.n1': 'Log of creates / price & stock updates.',
  'help.erpChanges.tabs.body':
    'Sync events (Created, Updated, Price, Stock).\nExcel import and ERP sync feed this audit log.',
  'help.erpChanges.tabs.rules':
    'RG-CH1|Each sync leaves a trail.\nRG-CH2|Excel import may be followed by enrichment.\nRG-CH3|Useful for Invoice↔ERP price gaps.',
  'help.erpChanges.tabs.example': 'E.g. Price VIS-M6 0.12 → 0.14 on 03/08 after sync.',
  'help.erpChanges.tabs.guide':
    'Filter by change type\nRead before/after\nRe-run sync or import if catalog is stale',
  'help.erpChanges.tabs.version': 'v1.0.0',

  'help.admin.tabs.title': 'Administration',
  'help.admin.tabs.n1': 'Tenants, companies, roles, users and help CMS.',
  'help.admin.tabs.body':
    'Multi-company setup: organization (tenant), companies, roles/permissions, user accounts.\nThe Help tab publishes CMS content that overrides i18n.',
  'help.admin.tabs.rules':
    'RG-AU1|Central auth / password policy.\nRG-AU2|Strong passwords.\nRG-HA1|Rights by function × object × company.\nRG-HA2|CRUDV matrix.',
  'help.admin.tabs.example': 'E.g. Accountant role → Accounting.*, Invoice.Read; no Cash.Manage.',
  'help.admin.tabs.guide':
    'Create/activate tenant\nCreate companies\nDefine roles and permissions\nCreate users and assign companies\nPublish help CMS if needed',
  'help.admin.tabs.version': 'v1.0.0',

  'help.admin.tenant.title': 'Tenant',
  'help.admin.tenant.n1': 'Organization grouping companies and users.',
  'help.admin.tenant.body': 'Manage tenant name and activation.',
  'help.admin.tenant.version': 'v1.0.0',

  'help.admin.company.title': 'Company',
  'help.admin.company.n1': 'Operating entity (documents, stock, cash).',
  'help.admin.company.body': 'Company under a tenant: name, language, currency.',
  'help.admin.company.version': 'v1.0.0',

  'help.admin.roles.title': 'Roles and permissions',
  'help.admin.roles.n1': 'CRUDV matrix by domain.',
  'help.admin.roles.body':
    'A role groups permissions (e.g. Invoice.Create).\nAssign roles using least privilege.',
  'help.admin.roles.rules': 'RG-HA1|Granularity function × object.\nRG-HA2|CRUDV per business object.',
  'help.admin.roles.version': 'v1.0.0',

  'help.admin.user.title': 'User',
  'help.admin.user.n1': 'Login account.',
  'help.admin.user.body': 'Username, roles and company access.',
  'help.admin.user.version': 'v1.0.0',

  'help.admin.resetPassword.title': 'Reset password',
  'help.admin.resetPassword.n1': 'New admin-set password.',
  'help.admin.resetPassword.body': 'Sets a new password for the selected user.',
  'help.admin.resetPassword.rules': 'RG-AU2|Respect complexity policy.',
  'help.admin.resetPassword.version': 'v1.0.0',

  'help.admin.assign.title': 'Assign companies',
  'help.admin.assign.n1': 'User multi-company scope.',
  'help.admin.assign.body': 'Choose the companies the user can access.',
  'help.admin.assign.version': 'v1.0.0',

  'help.admin.helpCms.title': 'Help CMS',
  'help.admin.helpCms.n1': 'Publish help articles.',
  'help.admin.helpCms.body':
    'Publish content that overrides i18n for a help key and language.\nRequires Help.Manage.',
  'help.admin.helpCms.guide':
    'Pick the key (e.g. sales.invoice)\nWrite title, body, rules, guide\nPublish\nCheck in help center (F1)',
  'help.admin.helpCms.version': 'v1.0.0',

  'help.numbering.tabs.title': 'Document numbering',
  'help.numbering.tabs.n1': 'Sequences FAC, DN, SO… per company.',
  'help.numbering.tabs.body':
    'Each document type has a format (prefix, year, counter).\nFinal numbers are assigned on validation.',
  'help.numbering.tabs.rules':
    'RG-T1|Chronological numbering without gaps after validation.\nRG-NUM1|Format via tokens {Prefix}, {Year}, {Number:D4}.\nRG-NUM2|Counter per company and document type.',
  'help.numbering.tabs.example': 'E.g. FAC-{Year}-{Number:D4} → FAC-2026-0001.',
  'help.numbering.tabs.guide':
    'Initialize default sequences if needed\nAdjust format\nPreview\nSave',
  'help.numbering.tabs.version': 'v1.0.0',

  'help.createProduct.title': 'Create ERP product',
  'help.createProduct.n1': 'Item from an OCR document line.',
  'help.createProduct.body':
    'Creates an item (reference, EAN, price, brand, categories) when parsing cannot find the product.',
  'help.createProduct.rules': 'RG-PR1|Unique reference.\nRG-PR3|Create from document line allowed.',
  'help.createProduct.guide':
    'Check reference and EAN\nFill price and label\nPick brand/categories\nCreate then re-run matching',
  'help.createProduct.version': 'v1.0.0',

  'help.field.upload.supplier.title': 'Supplier',
  'help.field.upload.supplier.n1': 'Party on the uploaded PDF.',
  'help.field.upload.supplier.body': 'Helps filter unlinked documents and association.',

  'help.field.cash.openingBalance.title': 'Opening float',
  'help.field.cash.openingBalance.n1': 'Cash at session start.',
  'help.field.cash.openingBalance.body': 'Counted drawer amount when opening the session.',

  'help.field.stock.quantity.title': 'Stock quantity',
  'help.field.stock.quantity.n1': 'New quantity after adjustment.',
  'help.field.stock.quantity.body': 'Must match physical count. A variance movement is created.',

  'help.field.accounting.account.title': 'GL account',
  'help.field.accounting.account.n1': 'Account number on the line.',
  'help.field.accounting.account.body': 'Use the chart of accounts (e.g. 411 customers, 401 suppliers, 512 bank).',

  'help.field.numbering.format.title': 'Number format',
  'help.field.numbering.format.n1': 'Pattern with tokens.',
  'help.field.numbering.format.body': 'Tokens: {Prefix}, {Year}, {Number:D4}… Preview before saving.'
};
