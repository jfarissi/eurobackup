/** Aide contextuelle par onglet Ventes / Achats (surcharge les stubs title+body). */
export type HelpDict = Record<string, string>;

export const HELP_SALES_PURCHASES_TABS_FR: HelpDict = {
  // ── Ventes : enrichissement onglets ─────────────────────────────────────
  'help.sales.deliveryNote.title': 'Bons de livraison (BL)',
  'help.sales.deliveryNote.n1': 'Livraison client ; impact stock à la validation.',
  'help.sales.deliveryNote.body':
    'Liste des BL clients. Créez un BL depuis une commande confirmée (livraison partielle possible).\nÀ la validation : sortie de stock et base pour facturation.',
  'help.sales.deliveryNote.rules':
    'RG-BL1|BL issu d’une commande confirmée (sauf cas exceptionnel).\nRG-BL2|Livraison partielle autorisée.\nRG-MS1|Validation BL = sortie de stock.\nRG-FC1|Un ou plusieurs BL peuvent générer une facture.',
  'help.sales.deliveryNote.example': 'Ex: Commande 10 pcs, stock 4 → BL de 4, reliquat 6.',
  'help.sales.deliveryNote.guide':
    'Ouvrir l’onglet BL\nCréer depuis une commande (qtés restantes)\nContrôler lignes et adresse\nValider → stock\nFacturer le(s) BL livré(s)',
  'help.sales.deliveryNote.version': 'v1.1.0',

  'help.sales.creditNote.title': 'Avoirs clients',
  'help.sales.creditNote.n1': 'Correction / retour sur facture ; réduit le reste dû.',
  'help.sales.creditNote.body':
    'Liste des avoirs. Création depuis une facture, un BRC, ou manuellement.\nAprès validation, l’avoir s’impute sur le solde de la facture (et le solde client).',
  'help.sales.creditNote.rules':
    'RG-AV1|Avoir lié à une facture du même client.\nRG-AV2|Total avoirs ≤ TTC facture (capacité restante).\nRG-AV3|Validation → impact comptable + solde facture.\nRG-AV4|Modification interdite après validation.',
  'help.sales.creditNote.example': 'Ex: Facture 100 €, avoir déjà 30 € → nouvel avoir max 70 €.',
  'help.sales.creditNote.guide':
    'Choisir la facture source (ou partir d’un BRC)\nSaisir / ajuster les lignes\nContrôler le plafond facture\nValider l’avoir\nVérifier reste dû facture',
  'help.sales.creditNote.version': 'v1.1.0',

  'help.sales.customer.title': 'Répertoire clients',
  'help.sales.customer.n1': 'Fiches tiers destinataires des documents de vente.',
  'help.sales.customer.body':
    'Créez et maintenez les clients (identité, adresse, TVA, conditions de paiement, plafond de crédit).\nUn client actif est requis pour devis, commandes et factures.',
  'help.sales.customer.rules':
    'RG-CC1|Tiers obligatoire et actif sur les documents.\nRG-CC2|Contrôle plafond crédit à la validation commande.\nRG-CC9|Client figé sur document après validation.',
  'help.sales.customer.example': 'Ex: Dupont SARL — plafond 15 000 €, encours 8 200 €.',
  'help.sales.customer.guide':
    'Créer ou ouvrir une fiche\nRenseigner TVA et conditions\nDéfinir le plafond si besoin\nEnregistrer\nUtiliser le client sur un devis/commande',
  'help.sales.customer.version': 'v1.1.0',

  'help.sales.return.title': 'Retours clients (BRC)',
  'help.sales.return.n1': 'Retour marchandise ; peut générer un avoir plafonné.',
  'help.sales.return.body':
    'Bons de retour client liés à un BL d’origine.\nIntégration stock (qualité) puis création d’avoir éventuelle, limitée à la capacité restante de la facture.',
  'help.sales.return.rules':
    'RG-BRC1|BRC rattaché à un BL livré/facturé.\nRG-ME3|Retour client = entrée stock selon qualité.\nRG-AV2|Avoir depuis BRC plafonné au reste facture.',
  'help.sales.return.example': 'Ex: Facture 49,34 €, avoir déjà 14,39 € → avoir BRC max ≈ 34,95 €.',
  'help.sales.return.guide':
    'Sélectionner le BL d’origine\nSaisir les quantités retournées\nIntégrer le stock\nCréer l’avoir si besoin (reste facture)\nValider l’avoir',
  'help.sales.return.version': 'v1.1.0',

  'help.sales.proforma.title': 'Proformas',
  'help.sales.proforma.n1': 'Document non comptable (devis ferme / douane / acompte).',
  'help.sales.proforma.body':
    'Factures proforma sans écriture comptable. Utiles pour engagement commercial, douane ou demande d’acompte.',
  'help.sales.proforma.rules':
    'RG-PF1|Pas d’impact comptable ni stock.\nRG-PF2|Peut précéder une facture définitive ou un acompte.',
  'help.sales.proforma.guide':
    'Créer la proforma (client + lignes)\nEnvoyer au client\nConvertir / facturer selon le processus',
  'help.sales.proforma.version': 'v1.1.0',

  'help.sales.deposit.title': 'Acomptes',
  'help.sales.deposit.n1': 'Factures d’acompte avant livraison complète.',
  'help.sales.deposit.body':
    'Gérez les factures d’acompte émises avant la facture finale.\nUn acompte validé peut être imputé pour réduire le solde de la facture finale.\nCommandes éligibles : Confirmed, PartiallyDelivered, Closed (pas Draft / Pending / Cancelled).',
  'help.sales.deposit.rules':
    'RG-AA1|Acompte toujours lié à une commande éligible.\nRG-ER3|Acompte encaissé avant livraison complète, compte d’attente 419.\nRG-AC1|Imputation ≤ montant acompte restant et ≤ reste dû facture.',
  'help.sales.deposit.example': 'Ex: Acompte 500 € → facture finale 2 000 € → reste dû 1 500 € après imputation.',
  'help.sales.deposit.guide':
    'Choisir une commande Confirmed / PartiallyDelivered / Closed\nÉmettre / valider l’acompte\nEncaisser si besoin\nSur la facture finale : imputer l’acompte\nContrôler le reste dû',
  'help.sales.deposit.version': 'v1.2.0',

  'help.sales.payment.title': 'Paiements',
  'help.sales.payment.n1': 'Encaissements liés aux factures clients.',
  'help.sales.payment.body':
    'Histor et saisie des règlements (espèces, carte, chèque, virement).\nLe paiement réduit le reste dû ; la facture passe PartiallyPaid puis Paid.',
  'help.sales.payment.rules':
    'RG-ER1|Mode de règlement hérité du client (modifiable).\nRG-ER8|Imputation sur factures (FIFO ou choix).\nRG-PAY1|Facture validée + BL livré requis pour payer.\nRG-PAY2|Montant ≤ reste dû (règlements + avoirs).',
  'help.sales.payment.example': 'Ex: Facture 51,40 € TTC, paiement 51,40 € → statut Paid, reste 0.',
  'help.sales.payment.guide':
    'Ouvrir une facture à régler (ou cet onglet)\nSaisir montant et mode\nEnregistrer\nContrôler statut / reste dû\nAnnuler un paiement si erreur (selon droits)',
  'help.sales.payment.version': 'v1.1.0',

  'help.sales.pilotage.title': 'Pilotage ventes',
  'help.sales.pilotage.n1': 'Indicateurs et suivi opérationnel du cycle vente.',
  'help.sales.pilotage.body':
    'Vue de pilotage : volumes, retards, alertes (crédit, stock, échéances).\nComplète les onglets documents sans les remplacer.',
  'help.sales.pilotage.rules':
    'RG-PI1|Les indicateurs sont calculés sur la société active.\nRG-RG9|Factures échues non soldées visibles pour relance.',
  'help.sales.pilotage.guide':
    'Consulter les KPI / listes\nOuvrir le document concerné depuis l’alerte\nTraiter (paiement, relance, livraison)',
  'help.sales.pilotage.version': 'v1.0.0',

  'help.sales.trash.title': 'Corbeille ventes',
  'help.sales.trash.n1': 'Documents soft-deleted (brouillons) à restaurer ou purger.',
  'help.sales.trash.body':
    'Liste des devis, commandes, BL et factures marqués supprimés.\nRestauration possible si le statut le permet et sans conflit métier (doublon, capacité).',
  'help.sales.trash.rules':
    'RG-TR1|Soft-delete = conservation technique, hors listes actives.\nRG-TR2|Restauration brouillon si numéro libre et règles métier OK.\nRG-TR3|Purge définitive = irréversible.',
  'help.sales.trash.guide':
    'Filtrer la corbeille\nRestaurer un brouillon si besoin\nOu purger / vider la corbeille',
  'help.sales.trash.version': 'v1.0.0',

  // ── Achats : onglets ────────────────────────────────────────────────────
  'help.purchases.rfq.title': 'Demandes de prix (DPF)',
  'help.purchases.rfq.n1': 'Cotation fournisseur avant commande.',
  'help.purchases.rfq.body':
    'Demandes de prix envoyées aux fournisseurs. Les lignes décrivent articles et quantités souhaités.\nUne DPF acceptée peut déboucher sur une CDF.',
  'help.purchases.rfq.rules':
    'RG-DPF1|Fournisseur et au moins une ligne.\nRG-DPF2|Pas d’impact stock ni comptable.',
  'help.purchases.rfq.guide':
    'Créer une DPF\nAjouter lignes et fournisseur\nEnvoyer / suivre la réponse\nTransformer en CDF si OK',
  'help.purchases.rfq.version': 'v1.1.0',

  'help.purchases.purchaseOrder.title': 'Commandes fournisseurs (CDF)',
  'help.purchases.purchaseOrder.n1': 'Engagement d’achat ; base réceptions et matching.',
  'help.purchases.purchaseOrder.body':
    'Liste des commandes fournisseurs. Création manuelle ou depuis une DPF.\nLes réceptions et factures se rapprochent de la CDF (quantités / prix).',
  'help.purchases.purchaseOrder.rules':
    'RG-CDF1|Fournisseur obligatoire + lignes.\nRG-CDF2|Réceptions ≤ quantités commandées (selon paramétrage).\nRG-AF matching|Écarts facture/commande peuvent exiger approbation.',
  'help.purchases.purchaseOrder.example': 'Ex: CDF 100 pcs → réception 40 puis 60.',
  'help.purchases.purchaseOrder.guide':
    'Créer / ouvrir une CDF\nValider la commande\nRéceptionner les BL\nRapprocher / comptabiliser les factures',
  'help.purchases.purchaseOrder.version': 'v1.0.0',

  'help.purchases.receipts.title': 'Réceptions',
  'help.purchases.receipts.n1': 'Entrées marchandises ; alimentent le stock.',
  'help.purchases.receipts.body':
    'Réceptions liées aux BL fournisseur / CDF.\nLa validation enregistre l’entrée en stock (selon règles) et prépare le rapprochement facture.',
  'help.purchases.receipts.rules':
    'RG-ME1|Entrée stock à validation réception.\nRG-BL stock|Quantités cohérentes avec CDF / BL.\nRG-FF1|Facture F référence commande et/ou BR.',
  'help.purchases.receipts.guide':
    'Depuis une CDF ou un doc parsé\nRéceptionner le BL\nContrôler quantités\nVérifier le stock\nComptabiliser la facture associée',
  'help.purchases.receipts.version': 'v1.0.0',

  'help.purchases.supplierInvoice.title': 'Factures fournisseurs',
  'help.purchases.supplierInvoice.n1': 'Pièces F validées ; écriture fournisseur / achats / TVA.',
  'help.purchases.supplierInvoice.body':
    'Factures fournisseurs manuelles ou issues de documents OCR.\nAprès validation : écriture comptable et rapprochement éventuel avec CDF / réceptions.',
  'help.purchases.supplierInvoice.rules':
    'RG-FF1|Référence commande et/ou BR.\nRG-FF|Validation = pièce fournisseur.\nRG-AF matching|Contrôle écarts prix/qté.',
  'help.purchases.supplierInvoice.guide':
    'Saisir ou comptabiliser depuis OCR\nLier CDF / BL si possible\nContrôler totaux et TVA\nValider\nSuivre le paiement fournisseur',
  'help.purchases.supplierInvoice.version': 'v1.0.0',

  'help.purchases.supplierCreditNote.title': 'Avoirs fournisseurs (AF)',
  'help.purchases.supplierCreditNote.n1': 'Avoirs reçus du fournisseur (retour, litige, remise).',
  'help.purchases.supplierCreditNote.body':
    'Avoirs fournisseurs, souvent suite à un BRF ou une réclamation.\nIls réduisent la dette fournisseur et peuvent être liés à une facture F.',
  'help.purchases.supplierCreditNote.rules':
    'RG-AF1|Même fournisseur que la facture liée.\nRG-AF2|Montant cohérent avec le litige / retour.',
  'help.purchases.supplierCreditNote.guide':
    'Créer l’AF (ou depuis BRF)\nLier la facture F si besoin\nValider\nContrôler le solde fournisseur',
  'help.purchases.supplierCreditNote.version': 'v1.0.0',

  'help.purchases.supplier.title': 'Répertoire fournisseurs',
  'help.purchases.supplier.n1': 'Fiches tiers acheteurs.',
  'help.purchases.supplier.body':
    'Créez et maintenez les fournisseurs (code, nom, TVA, coordonnées).\nRequis pour DPF, CDF, réceptions et factures F.',
  'help.purchases.supplier.rules': 'RG-FO1|Fournisseur actif obligatoire sur les documents achat.',
  'help.purchases.supplier.guide':
    'Créer / éditer la fiche\nRenseigner TVA et coordonnées\nUtiliser sur CDF / factures / OCR',
  'help.purchases.supplier.version': 'v1.1.0',

  'help.purchases.supplierReturn.title': 'Retours fournisseurs (BRF)',
  'help.purchases.supplierReturn.n1': 'Retour marchandise vers le fournisseur.',
  'help.purchases.supplierReturn.body':
    'Bons de retour fournisseur. Peuvent précéder un avoir AF et génèrent une sortie de stock.',
  'help.purchases.supplierReturn.rules':
    'RG-MS3|Retour fournisseur = sortie stock (CMP).\nRG-AF|Peut générer un avoir fournisseur.',
  'help.purchases.supplierReturn.guide':
    'Créer le BRF\nSélectionner articles / quantités\nValider (stock)\nCréer l’AF si accord fournisseur',
  'help.purchases.supplierReturn.version': 'v1.1.0',

  'help.purchases.parsedDocuments.title': 'Documents parsés (OCR)',
  'help.purchases.parsedDocuments.n1': 'PDF fournisseurs extraits → comptabilisation.',
  'help.purchases.parsedDocuments.body':
    'Documents uploadés et parsés (factures, BL).\nAssociez-les (Compare), puis comptabilisez un document ou un lot facture+BL.',
  'help.purchases.parsedDocuments.rules':
    'RG-FF1|Réception électronique (OCR) autorisée.\nRG-OCR1|Vérifier fournisseur et lignes avant compta.\nCompare|Lier facture↔BL avant lot.\nRG-BL stock|Réception peut alimenter le stock.',
  'help.purchases.parsedDocuments.example': 'Ex: FAC #42 + BL #10/#11 → lot → 2 réceptions + 1 FF.',
  'help.purchases.parsedDocuments.guide':
    'Uploader le PDF (menu Upload)\nVérifier le parsing ici\nAssocier Facture↔BL si besoin\nComptabiliser le document ou le lot\nContrôler réceptions / facture F',
  'help.purchases.parsedDocuments.version': 'v1.0.0'
};

export const HELP_SALES_PURCHASES_TABS_NL: HelpDict = {
  'help.sales.deliveryNote.title': 'Leveringsbonnen (LB)',
  'help.sales.deliveryNote.n1': 'Klantlevering; voorraad bij validatie.',
  'help.sales.deliveryNote.body':
    'Lijst van klant-LB’s. Maak een LB vanuit een bevestigde bestelling (gedeeltelijke levering mogelijk).\nBij validatie: voorraaduitgang en basis voor facturatie.',
  'help.sales.deliveryNote.rules':
    'RG-BL1|LB vanuit bevestigde bestelling.\nRG-BL2|Gedeeltelijke levering toegestaan.\nRG-MS1|Validatie LB = voorraaduitgang.\nRG-FC1|Eén of meer LB’s kunnen een factuur vormen.',
  'help.sales.deliveryNote.example': 'Bv: Order 10 st, voorraad 4 → LB van 4, rest 6.',
  'help.sales.deliveryNote.guide':
    'Tab LB openen\nAanmaken vanuit bestelling\nRegels controleren\nValideren → voorraad\nGeleverde LB(s) factureren',
  'help.sales.deliveryNote.version': 'v1.1.0',

  'help.sales.creditNote.title': 'Klantcreditnota\'s',
  'help.sales.creditNote.n1': 'Correctie/retour op factuur; verlaagt openstaand.',
  'help.sales.creditNote.body':
    'Lijst creditnota’s. Vanuit factuur, BRC of manueel.\nNa validatie vermindert de creditnota het factuursaldo.',
  'help.sales.creditNote.rules':
    'RG-AV1|Gekoppeld aan factuurzelfde klant.\nRG-AV2|Totaal creditnota’s ≤ factuur incl.\nRG-AV3|Validatie → boekhouding + saldo.\nRG-AV4|Geen wijziging na validatie.',
  'help.sales.creditNote.example': 'Bv: Factuur 100 €, credit 30 € → nieuw max 70 €.',
  'help.sales.creditNote.guide':
    'Bronfactuur kiezen (of BRC)\nRegels invoeren\nPlafond controleren\nValideren\nOpenstaand controleren',
  'help.sales.creditNote.version': 'v1.1.0',

  'help.sales.customer.title': 'Klantenlijst',
  'help.sales.customer.n1': 'Klantfiches voor verkoopdocumenten.',
  'help.sales.customer.body':
    'Beheer klanten (identiteit, adres, btw, betalingsvoorwaarden, kredietplafond).\nActieve klant vereist voor offertes, orders en facturen.',
  'help.sales.customer.rules':
    'RG-CC1|Actieve partij verplicht.\nRG-CC2|Kredietplafond bij ordervalidatie.\nRG-CC9|Klant vast na validatie.',
  'help.sales.customer.example': 'Bv: Dupont SARL — plafond 15 000 €, openstaand 8 200 €.',
  'help.sales.customer.guide':
    'Fiche maken/openen\nBtw en voorwaarden\nPlafond instellen\nOpslaan\nGebruiken op offerte/order',
  'help.sales.customer.version': 'v1.1.0',

  'help.sales.return.title': 'Klantretouren (BRC)',
  'help.sales.return.n1': 'Goederenretour; kan begrensde creditnota maken.',
  'help.sales.return.body':
    'Retourbonnen gekoppeld aan een bron-LB.\nVoorraadintegratie daarna eventueel creditnota beperkt tot restcapaciteit factuur.',
  'help.sales.return.rules':
    'RG-BRC1|BRC op geleverde/gefactureerde LB.\nRG-ME3|Retour = voorraadingang volgens kwaliteit.\nRG-AV2|Credit vanuit BRC begrensd tot rest factuur.',
  'help.sales.return.example': 'Bv: Factuur 49,34 €, credit 14,39 € → BRC-credit max ≈ 34,95 €.',
  'help.sales.return.guide':
    'Bron-LB kiezen\nRetourhoeveelheden\nVoorraad integreren\nCreditnota indien nodig\nCreditnota valideren',
  'help.sales.return.version': 'v1.1.0',

  'help.sales.proforma.title': 'Proforma\'s',
  'help.sales.proforma.n1': 'Niet-boekhoudkundig document.',
  'help.sales.proforma.body': 'Proforma’s zonder boeking. Voor vaste offerte, douane of voorschot.',
  'help.sales.proforma.rules': 'RG-PF1|Geen boekhoud-/voorraadimpact.\nRG-PF2|Kan voorafgaan aan definitieve factuur of voorschot.',
  'help.sales.proforma.guide': 'Proforma maken\nNaar klant sturen\nOmzetten/factureren volgens proces',
  'help.sales.proforma.version': 'v1.1.0',

  'help.sales.deposit.title': 'Voorschotten',
  'help.sales.deposit.n1': 'Voorschotfacturen vóór volledige levering.',
  'help.sales.deposit.body': 'Beheer voorschotten. Een gevalideerd voorschot kan op de eindfactuur worden verrekend.\nGeschikte bestellingen: Confirmed, PartiallyDelivered, Closed (geen Draft / Pending / Cancelled).',
  'help.sales.deposit.rules':
    'RG-AA1|Voorschot altijd gekoppeld aan een geschikte bestelling.\nRG-ER3|Voorschot op wachtrekening dan verrekening.\nRG-AC1|Verrekening ≤ rest voorschot en ≤ openstaand factuur.',
  'help.sales.deposit.example': 'Bv: Voorschot 500 € → eindfactuur 2 000 € → openstaand 1 500 €.',
  'help.sales.deposit.guide': 'Bestelling Confirmed / PartiallyDelivered / Closed kiezen\nVoorschot uitgeven/valideren\nInnen\nOp eindfactuur verrekenen\nOpenstaand controleren',
  'help.sales.deposit.version': 'v1.2.0',

  'help.sales.payment.title': 'Betalingen',
  'help.sales.payment.n1': 'Ontvangsten op klantfacturen.',
  'help.sales.payment.body':
    'Overzicht en registratie van betalingen. Vermindert openstaand; factuur → PartiallyPaid → Paid.',
  'help.sales.payment.rules':
    'RG-ER1|Betaalwijze van klant.\nRG-ER8|Imputatie op facturen.\nRG-PAY1|Gevalideerde factuur + geleverde LB.\nRG-PAY2|Bedrag ≤ openstaand.',
  'help.sales.payment.example': 'Bv: Factuur 51,40 €, betaling 51,40 € → Paid.',
  'help.sales.payment.guide':
    'Factuur openen of dit tabblad\nBedrag en wijze\nOpslaan\nStatus controleren\nBetaling annuleren indien fout',
  'help.sales.payment.version': 'v1.1.0',

  'help.sales.pilotage.title': 'Verkoopsturing',
  'help.sales.pilotage.n1': 'KPI’s en operationele opvolging.',
  'help.sales.pilotage.body': 'Sturing: volumes, achterstanden, alerts (krediet, voorraad, vervaldagen).',
  'help.sales.pilotage.rules': 'RG-PI1|Indicatoren op actief bedrijf.\nRG-RG9|Vervallen openstaande facturen voor herinnering.',
  'help.sales.pilotage.guide': 'KPI’s bekijken\nDocument openen vanuit alert\nAfhandelen',
  'help.sales.pilotage.version': 'v1.0.0',

  'help.sales.trash.title': 'Prullenbak verkopen',
  'help.sales.trash.n1': 'Soft-deleted documenten herstellen of wissen.',
  'help.sales.trash.body':
    'Verwijderde offertes, orders, LB’s en facturen.\nHerstel mogelijk als status en bedrijfsregels het toelaten.',
  'help.sales.trash.rules':
    'RG-TR1|Soft-delete = technische bewaring.\nRG-TR2|Herstel concept indien nummer vrij.\nRG-TR3|Definitief wissen = onomkeerbaar.',
  'help.sales.trash.guide': 'Filteren\nHerstellen of purgeren',
  'help.sales.trash.version': 'v1.0.0',

  'help.purchases.rfq.title': 'Prijsaanvragen (DPF)',
  'help.purchases.rfq.n1': 'Offerteaanvraag vóór bestelling.',
  'help.purchases.rfq.body': 'DPF’s naar leveranciers. Aanvaarde DPF kan CDF worden.',
  'help.purchases.rfq.rules': 'RG-DPF1|Leverancier + minstens één regel.\nRG-DPF2|Geen voorraad-/boekhoudimpact.',
  'help.purchases.rfq.guide': 'DPF maken\nRegels toevoegen\nOpvolgen\nOmzetten naar CDF',
  'help.purchases.rfq.version': 'v1.1.0',

  'help.purchases.purchaseOrder.title': 'Leveranciersbestellingen (CDF)',
  'help.purchases.purchaseOrder.n1': 'Aankoopverbintenis; basis ontvangsten/matching.',
  'help.purchases.purchaseOrder.body': 'CDF-lijst. Ontvangsten en facturen worden afgestemd op de CDF.',
  'help.purchases.purchaseOrder.rules':
    'RG-CDF1|Leverancier + regels.\nRG-CDF2|Ontvangsten ≤ besteld.\nRG-AF matching|Afwijkingen kunnen goedkeuring vereisen.',
  'help.purchases.purchaseOrder.example': 'Bv: CDF 100 → ontvangst 40 dan 60.',
  'help.purchases.purchaseOrder.guide': 'CDF maken\nValideren\nLB’s ontvangen\nFacturen matchen/boeken',
  'help.purchases.purchaseOrder.version': 'v1.0.0',

  'help.purchases.receipts.title': 'Ontvangsten',
  'help.purchases.receipts.n1': 'Goedereningang; voedt voorraad.',
  'help.purchases.receipts.body': 'Ontvangsten gekoppeld aan leveranciers-LB / CDF. Validatie = voorraadingang.',
  'help.purchases.receipts.rules':
    'RG-ME1|Voorraad bij validatie ontvangst.\nRG-BL stock|Hoeveelheden vs CDF/LB.\nRG-FF1|Factuur F refereert order en/of BR.',
  'help.purchases.receipts.guide': 'Vanuit CDF of OCR\nLB ontvangen\nHoeveelheden controleren\nVoorraad checken\nFactuur boeken',
  'help.purchases.receipts.version': 'v1.0.0',

  'help.purchases.supplierInvoice.title': 'Leveranciersfacturen',
  'help.purchases.supplierInvoice.n1': 'F-stukken; boeking leverancier/aankopen/btw.',
  'help.purchases.supplierInvoice.body': 'Manuele of OCR-facturen. Na validatie: boekhouding en matching.',
  'help.purchases.supplierInvoice.rules':
    'RG-FF1|Referentie order en/of BR.\nRG-FF|Validatie = leveranciersstuk.\nRG-AF matching|Prijs/qté-controle.',
  'help.purchases.supplierInvoice.guide': 'Invoeren of OCR boeken\nCDF/LB koppelen\nTotalen controleren\nValideren\nBetaling opvolgen',
  'help.purchases.supplierInvoice.version': 'v1.0.0',

  'help.purchases.supplierCreditNote.title': 'Leverancierscreditnota\'s (AF)',
  'help.purchases.supplierCreditNote.n1': 'Creditnota’s van leverancier.',
  'help.purchases.supplierCreditNote.body': 'Vaak na BRF of claim. Verminderen leveranciersschuld.',
  'help.purchases.supplierCreditNote.rules': 'RG-AF1|Zelfde leverancier.\nRG-AF2|Bedrag coherent met retour/litige.',
  'help.purchases.supplierCreditNote.guide': 'AF maken\nFactuur F koppelen\nValideren\nSaldo controleren',
  'help.purchases.supplierCreditNote.version': 'v1.0.0',

  'help.purchases.supplier.title': 'Leverancierslijst',
  'help.purchases.supplier.n1': 'Leveranciersfiches.',
  'help.purchases.supplier.body': 'Beheer leveranciers. Vereist voor DPF, CDF, ontvangsten en F-facturen.',
  'help.purchases.supplier.rules': 'RG-FO1|Actieve leverancier verplicht.',
  'help.purchases.supplier.guide': 'Fiche maken\nBtw/contact\nGebruiken op documenten',
  'help.purchases.supplier.version': 'v1.1.0',

  'help.purchases.supplierReturn.title': 'Leveranciersretouren (BRF)',
  'help.purchases.supplierReturn.n1': 'Retour naar leverancier.',
  'help.purchases.supplierReturn.body': 'BRF’s. Kunnen AF voorafgaan en geven voorraaduitgang.',
  'help.purchases.supplierReturn.rules': 'RG-MS3|Retour = voorraaduitgang.\nRG-AF|Kan AF genereren.',
  'help.purchases.supplierReturn.guide': 'BRF maken\nHoeveelheden\nValideren\nAF indien akkoord',
  'help.purchases.supplierReturn.version': 'v1.1.0',

  'help.purchases.parsedDocuments.title': 'Geparsede documenten (OCR)',
  'help.purchases.parsedDocuments.n1': 'Geëxtraheerde PDF’s → boeking.',
  'help.purchases.parsedDocuments.body':
    'Geüploade documenten. Koppelen (Compare), daarna document of lot factuur+LB boeken.',
  'help.purchases.parsedDocuments.rules':
    'RG-FF1|OCR toegestaan.\nRG-OCR1|Leverancier/regels controleren.\nCompare|Factuur↔LB vóór lot.\nRG-BL stock|Ontvangst kan voorraad voeden.',
  'help.purchases.parsedDocuments.example': 'Bv: FAC #42 + LB #10/#11 → lot.',
  'help.purchases.parsedDocuments.guide':
    'PDF uploaden\nParsing controleren\nAssociëren\nDocument/lot boeken\nOntvangsten/F controleren',
  'help.purchases.parsedDocuments.version': 'v1.0.0'
};

export const HELP_SALES_PURCHASES_TABS_EN: HelpDict = {
  'help.sales.deliveryNote.title': 'Delivery notes (DN)',
  'help.sales.deliveryNote.n1': 'Customer delivery; stock on validation.',
  'help.sales.deliveryNote.body':
    'Customer DN list. Create from a confirmed order (partial delivery allowed).\nOn validation: stock out and basis for invoicing.',
  'help.sales.deliveryNote.rules':
    'RG-BL1|DN from a confirmed order.\nRG-BL2|Partial delivery allowed.\nRG-MS1|DN validation = stock out.\nRG-FC1|One or more DNs can build an invoice.',
  'help.sales.deliveryNote.example': 'E.g. Order 10 pcs, stock 4 → DN of 4, backlog 6.',
  'help.sales.deliveryNote.guide':
    'Open DN tab\nCreate from order\nCheck lines\nValidate → stock\nInvoice delivered DN(s)',
  'help.sales.deliveryNote.version': 'v1.1.0',

  'help.sales.creditNote.title': 'Customer credit notes',
  'help.sales.creditNote.n1': 'Invoice correction/return; reduces amount due.',
  'help.sales.creditNote.body':
    'Credit note list. From invoice, BRC, or manual.\nAfter validation, settles invoice balance.',
  'help.sales.creditNote.rules':
    'RG-AV1|Linked to same-customer invoice.\nRG-AV2|Credits ≤ invoice TTC.\nRG-AV3|Validation → GL + balance.\nRG-AV4|No edit after validation.',
  'help.sales.creditNote.example': 'E.g. Invoice €100, credit €30 → new credit max €70.',
  'help.sales.creditNote.guide':
    'Pick source invoice (or BRC)\nEnter lines\nCheck cap\nValidate\nCheck remaining due',
  'help.sales.creditNote.version': 'v1.1.0',

  'help.sales.customer.title': 'Customer directory',
  'help.sales.customer.n1': 'Customer master for sales docs.',
  'help.sales.customer.body':
    'Maintain customers (identity, address, VAT, payment terms, credit limit).\nActive customer required for quotes, orders and invoices.',
  'help.sales.customer.rules':
    'RG-CC1|Active party required.\nRG-CC2|Credit limit on order validation.\nRG-CC9|Customer frozen after validation.',
  'help.sales.customer.example': 'E.g. Dupont SARL — limit €15,000, outstanding €8,200.',
  'help.sales.customer.guide':
    'Create/open record\nSet VAT and terms\nSet credit limit\nSave\nUse on quote/order',
  'help.sales.customer.version': 'v1.1.0',

  'help.sales.return.title': 'Customer returns (BRC)',
  'help.sales.return.n1': 'Goods return; may create capped credit.',
  'help.sales.return.body':
    'Returns linked to a source DN.\nStock integrate then optional credit capped to invoice remaining capacity.',
  'help.sales.return.rules':
    'RG-BRC1|BRC on delivered/invoiced DN.\nRG-ME3|Return = stock in by quality.\nRG-AV2|Credit from BRC capped to invoice remainder.',
  'help.sales.return.example': 'E.g. Invoice €49.34, credit €14.39 → BRC credit max ≈ €34.95.',
  'help.sales.return.guide':
    'Select source DN\nEnter return qtys\nIntegrate stock\nCreate credit if needed\nValidate credit',
  'help.sales.return.version': 'v1.1.0',

  'help.sales.proforma.title': 'Proformas',
  'help.sales.proforma.n1': 'Non-accounting document.',
  'help.sales.proforma.body': 'Proformas with no GL posting. For firm quote, customs or deposit request.',
  'help.sales.proforma.rules': 'RG-PF1|No stock/GL impact.\nRG-PF2|May precede final invoice or deposit.',
  'help.sales.proforma.guide': 'Create proforma\nSend to customer\nConvert/invoice per process',
  'help.sales.proforma.version': 'v1.1.0',

  'help.sales.deposit.title': 'Deposits',
  'help.sales.deposit.n1': 'Deposit invoices before full delivery.',
  'help.sales.deposit.body': 'Manage deposit invoices. A validated deposit can be applied to the final invoice.\nEligible orders: Confirmed, PartiallyDelivered, Closed (not Draft / Pending / Cancelled).',
  'help.sales.deposit.rules':
    'RG-AA1|Deposit always linked to an eligible order.\nRG-ER3|Deposit on suspense then apply.\nRG-AC1|Apply ≤ deposit remainder and ≤ invoice due.',
  'help.sales.deposit.example': 'E.g. Deposit €500 → final €2,000 → due €1,500 after apply.',
  'help.sales.deposit.guide': 'Pick Confirmed / PartiallyDelivered / Closed order\nIssue/validate deposit\nCollect payment\nApply on final invoice\nCheck remaining due',
  'help.sales.deposit.version': 'v1.2.0',

  'help.sales.payment.title': 'Payments',
  'help.sales.payment.n1': 'Receipts on customer invoices.',
  'help.sales.payment.body':
    'List and enter settlements. Reduces remaining due; invoice → PartiallyPaid → Paid.',
  'help.sales.payment.rules':
    'RG-ER1|Payment method from customer.\nRG-ER8|Allocation to invoices.\nRG-PAY1|Validated invoice + delivered DN.\nRG-PAY2|Amount ≤ remaining due.',
  'help.sales.payment.example': 'E.g. Invoice €51.40, payment €51.40 → Paid.',
  'help.sales.payment.guide':
    'Open invoice or this tab\nEnter amount and method\nSave\nCheck status\nCancel payment if needed',
  'help.sales.payment.version': 'v1.1.0',

  'help.sales.pilotage.title': 'Sales ops dashboard',
  'help.sales.pilotage.n1': 'KPIs and operational follow-up.',
  'help.sales.pilotage.body': 'Dashboard: volumes, delays, alerts (credit, stock, due dates).',
  'help.sales.pilotage.rules': 'RG-PI1|Indicators for active company.\nRG-RG9|Overdue unpaid invoices for reminders.',
  'help.sales.pilotage.guide': 'Review KPIs\nOpen document from alert\nAct',
  'help.sales.pilotage.version': 'v1.0.0',

  'help.sales.trash.title': 'Sales trash',
  'help.sales.trash.n1': 'Soft-deleted docs to restore or purge.',
  'help.sales.trash.body':
    'Deleted quotes, orders, DNs and invoices.\nRestore when status and business rules allow.',
  'help.sales.trash.rules':
    'RG-TR1|Soft-delete = technical retention.\nRG-TR2|Restore draft if number free.\nRG-TR3|Purge is irreversible.',
  'help.sales.trash.guide': 'Filter trash\nRestore or purge',
  'help.sales.trash.version': 'v1.0.0',

  'help.purchases.rfq.title': 'Requests for quote (RFQ)',
  'help.purchases.rfq.n1': 'Supplier quote before ordering.',
  'help.purchases.rfq.body': 'RFQs to suppliers. An accepted RFQ can become a PO.',
  'help.purchases.rfq.rules': 'RG-DPF1|Supplier + at least one line.\nRG-DPF2|No stock/GL impact.',
  'help.purchases.rfq.guide': 'Create RFQ\nAdd lines\nTrack reply\nConvert to PO',
  'help.purchases.rfq.version': 'v1.1.0',

  'help.purchases.purchaseOrder.title': 'Purchase orders (PO)',
  'help.purchases.purchaseOrder.n1': 'Purchase commitment; basis for receipts/matching.',
  'help.purchases.purchaseOrder.body': 'PO list. Receipts and invoices match against the PO.',
  'help.purchases.purchaseOrder.rules':
    'RG-CDF1|Supplier + lines.\nRG-CDF2|Receipts ≤ ordered.\nRG-AF matching|Variances may need approval.',
  'help.purchases.purchaseOrder.example': 'E.g. PO 100 → receive 40 then 60.',
  'help.purchases.purchaseOrder.guide': 'Create PO\nValidate\nReceive DNs\nMatch/post invoices',
  'help.purchases.purchaseOrder.version': 'v1.0.0',

  'help.purchases.receipts.title': 'Receipts',
  'help.purchases.receipts.n1': 'Goods in; feed stock.',
  'help.purchases.receipts.body': 'Receipts linked to supplier DN / PO. Validation = stock in.',
  'help.purchases.receipts.rules':
    'RG-ME1|Stock in on receipt validation.\nRG-BL stock|Qtys vs PO/DN.\nRG-FF1|Supplier invoice refs order and/or receipt.',
  'help.purchases.receipts.guide': 'From PO or OCR\nReceive DN\nCheck qtys\nCheck stock\nPost invoice',
  'help.purchases.receipts.version': 'v1.0.0',

  'help.purchases.supplierInvoice.title': 'Supplier invoices',
  'help.purchases.supplierInvoice.n1': 'Supplier bills; GL supplier/purchases/VAT.',
  'help.purchases.supplierInvoice.body': 'Manual or OCR invoices. After validation: posting and matching.',
  'help.purchases.supplierInvoice.rules':
    'RG-FF1|Reference order and/or receipt.\nRG-FF|Validation = supplier document.\nRG-AF matching|Price/qty check.',
  'help.purchases.supplierInvoice.guide': 'Enter or post from OCR\nLink PO/DN\nCheck totals\nValidate\nTrack payment',
  'help.purchases.supplierInvoice.version': 'v1.0.0',

  'help.purchases.supplierCreditNote.title': 'Supplier credit notes (AF)',
  'help.purchases.supplierCreditNote.n1': 'Credits from supplier.',
  'help.purchases.supplierCreditNote.body': 'Often after BRF or claim. Reduce supplier payable.',
  'help.purchases.supplierCreditNote.rules': 'RG-AF1|Same supplier.\nRG-AF2|Amount consistent with return/dispute.',
  'help.purchases.supplierCreditNote.guide': 'Create AF\nLink invoice\nValidate\nCheck balance',
  'help.purchases.supplierCreditNote.version': 'v1.0.0',

  'help.purchases.supplier.title': 'Supplier directory',
  'help.purchases.supplier.n1': 'Supplier master.',
  'help.purchases.supplier.body': 'Maintain suppliers. Required for RFQ, PO, receipts and invoices.',
  'help.purchases.supplier.rules': 'RG-FO1|Active supplier required.',
  'help.purchases.supplier.guide': 'Create record\nVAT/contact\nUse on documents',
  'help.purchases.supplier.version': 'v1.1.0',

  'help.purchases.supplierReturn.title': 'Supplier returns (BRF)',
  'help.purchases.supplierReturn.n1': 'Return to supplier.',
  'help.purchases.supplierReturn.body': 'BRFs. May precede AF and stock out.',
  'help.purchases.supplierReturn.rules': 'RG-MS3|Return = stock out.\nRG-AF|May generate AF.',
  'help.purchases.supplierReturn.guide': 'Create BRF\nQtys\nValidate\nAF if agreed',
  'help.purchases.supplierReturn.version': 'v1.1.0',

  'help.purchases.parsedDocuments.title': 'Parsed documents (OCR)',
  'help.purchases.parsedDocuments.n1': 'Extracted PDFs → posting.',
  'help.purchases.parsedDocuments.body':
    'Uploaded docs. Associate (Compare), then post a document or invoice+DN lot.',
  'help.purchases.parsedDocuments.rules':
    'RG-FF1|OCR allowed.\nRG-OCR1|Check supplier/lines.\nCompare|Invoice↔DN before lot.\nRG-BL stock|Receipt may feed stock.',
  'help.purchases.parsedDocuments.example': 'E.g. INV #42 + DN #10/#11 → lot.',
  'help.purchases.parsedDocuments.guide':
    'Upload PDF\nCheck parsing\nAssociate\nPost document/lot\nCheck receipts/invoice',
  'help.purchases.parsedDocuments.version': 'v1.0.0'
};
