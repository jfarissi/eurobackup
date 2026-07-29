import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export type AppLang = 'fr' | 'nl' | 'en';

const STORAGE_KEY = 'backup_app_lang';
const LEGACY_STORAGE_KEY = 'store_assistant_lang';

type Dict = Record<string, string>;

const FR: Dict = {
  // common
  'common.ok': 'OK',
  'common.close': 'Fermer',
  'common.cancel': 'Annuler',
  'common.reset': 'Réinitialiser',
  'common.refresh': 'Actualiser',
  'common.loading': 'Chargement…',
  'common.prev': 'Précédent',
  'common.next': 'Suivant',
  'common.search': 'Rechercher',
  'common.lang': 'Langue',
  'common.show': 'Afficher',
  'common.hide': 'Masquer',

  // nav
  'nav.brandSub': 'Industrial Logistics',
  'nav.newAnalysis': 'Nouvelle analyse',
  'nav.logout': 'Déconnexion',
  'nav.upload': 'Upload',
  'nav.search': 'Recherche',
  'nav.compare': 'Association',
  'nav.stock': 'Stock',
  'nav.erpProducts': 'Produits',
  'nav.erpChanges': 'Changements',
  'nav.assistant': 'Assistant',
  'nav.assistantTab': 'Magasin',
  'nav.sales': 'Ventes & Clients',
  'nav.purchases': 'Achats',
  'nav.cash': 'Caisse',
  'nav.numbering': 'Numérotation',
  'nav.admin': 'Administration',
  'nav.title.upload': 'Gestion Documents',
  'nav.title.search': 'Recherche',
  'nav.title.compare': 'Association',
  'nav.title.stock': 'Gestion Documents',
  'nav.title.erpProducts': 'Produits ERP',
  'nav.title.erpChanges': 'Changements ERP',
  'nav.title.assistant': 'Assistant magasin',
  'nav.title.sales': 'Ventes & Clients',
  'nav.title.purchases': 'Achats',
  'nav.title.cash': 'Caisse',
  'nav.title.numbering': 'Numérotation',
  'nav.title.admin': 'Administration',
  'nav.title.default': 'Gestion Documents',

  'accessDenied.title': 'Accès non autorisé',
  'accessDenied.message': "Votre compte n'a pas les permissions nécessaires pour cette page.",
  'accessDenied.asUser': 'Connecté en tant que',
  'accessDenied.goHome': "Aller à l'accueil",
  'accessDenied.logout': 'Se déconnecter',

  // login
  'login.brandSub': 'Connexion',
  'login.title': "Accéder à l'application",
  'login.hint': 'Utilisez votre compte EuroBrico Backup',
  'login.email': 'Email',
  'login.password': 'Mot de passe',
  'login.submit': 'Se connecter',
  'login.submitting': 'Connexion…',
  'login.required': 'Email et mot de passe requis',
  'login.invalid': 'Identifiants incorrects',

  // upload
  'upload.title': 'Upload de Documents',
  'upload.newDocument': 'Nouveau Document',
  'upload.dropPlaceholder': 'Glissez votre fichier ici',
  'upload.dropHint': 'Faites glisser votre fichier ici ou cliquez pour parcourir',
  'upload.documentType': 'Type de Document',
  'upload.type.invoice': 'Facture',
  'upload.type.deliveryNote': 'Bon de Livraison',
  'upload.type.other': 'Autre',
  'upload.supplier': 'Fournisseur',
  'upload.selectPlaceholder': '-- Sélectionner --',
  'upload.noSuppliersHint': "Aucun fournisseur enregistré. Le fournisseur sera ajouté lors de l'inspection du fichier.",
  'upload.number': 'Numéro',
  'upload.numberPlaceholder': 'Numéro du document',
  'upload.client': 'Client',
  'upload.clientPlaceholder': 'Nom du client',
  'upload.date': 'Date',
  'upload.aiEnabled': 'Extraction par IA activée',
  'upload.aiAccuracy': 'Précision ~99%',
  'upload.submit': 'Uploader',
  'upload.submitting': 'Upload en cours...',
  'upload.reset': 'Réinitialiser',
  'upload.recentHistory': 'Historique Récent',
  'upload.viewAll': 'Tout voir',
  'upload.col.document': 'Document',
  'upload.col.supplier': 'Fournisseur',
  'upload.col.status': 'Statut',
  'upload.col.date': 'Date',
  'upload.col.id': 'ID',
  'upload.col.type': 'Type',
  'upload.col.client': 'Client',
  'upload.unidentified': 'Non identifié',
  'upload.status.validated': 'Validé',
  'upload.status.error': 'Erreur',
  'upload.aiMetrics': 'Métriques IA',
  'upload.indexedDocuments': 'Documents indexés',
  'upload.unlinkedTitle': 'Documents Non Associés — {supplier}',
  'upload.download': 'Télécharger',
  'upload.relative.justNow': "À l'instant",
  'upload.relative.hoursAgo': 'Il y a {hours}h',
  'upload.relative.daysAgo': 'Il y a {days}j',
  'upload.snack.selectFile': 'Veuillez sélectionner un fichier',
  'upload.snack.success': 'Document uploadé avec succès',
  'upload.snack.duplicate': 'Ce document existe déjà dans le système',
  'upload.snack.uploadError': "Erreur lors de l'upload",
  'upload.snack.linkSuccess': 'BL associé à la facture avec succès',
  'upload.snack.linkError': "Erreur lors de l'association",
  'upload.snack.invoicesFound': "{count} factures trouvées. Redirection vers la page d'association...",
  'upload.snack.noInvoice': 'Aucune facture trouvée avec ce numéro. Affichage de toutes les factures du fournisseur...',
  'upload.confirm.linkInvoice': 'Facture trouvée : {numero}\nVoulez-vous l\'associer à ce BL ?',

  // search
  'search.title': 'Recherche de Documents',
  'search.subtitle': 'Rechercher par texte, numéro, client ou fournisseur',
  'search.placeholder': 'Rechercher un document...',
  'search.submit': 'Rechercher',
  'search.stat.results': 'Résultats',
  'search.stat.invoices': 'Factures',
  'search.stat.deliveryNotes': 'Bons de livraison',
  'search.activeFilter': 'Filtre actif : « {query} »',
  'search.loading': 'Recherche en cours…',
  'search.empty.welcome': 'Saisissez un terme puis cliquez sur Rechercher',
  'search.empty.hint': 'Numéro, client, fournisseur ou texte du document',
  'search.empty.none': 'Aucun document trouvé',
  'search.resultsTitle': 'Documents',
  'search.col.id': 'ID',
  'search.col.type': 'Type',
  'search.col.number': 'Numéro',
  'search.col.supplier': 'Fournisseur',
  'search.col.client': 'Client',
  'search.col.date': 'Date',
  'search.col.actions': 'Actions',
  'search.type.invoice': 'Facture',
  'search.type.bl': 'BL',
  'search.type.other': 'Autre',
  'search.tooltip.openAssociation': 'Ouvrir dans Association',
  'search.tooltip.download': 'Télécharger',
  'search.error.load': "Impossible de charger les documents. Vérifiez que l'API est démarrée.",

  // compare
  'compare.title': 'Association & Comparaison de Documents',
  'compare.subtitle': 'Réconciliation automatisée des factures et bons de livraison.',
  'compare.exportAll': 'Exporter tout (Excel)',
  'compare.validateAssociation': "Valider l'Association",
  'compare.suggestedInvoices': 'Factures suggérées pour BL #{blId}',
  'compare.selection.association': 'Sélection Actuelle (Association Facture-BL)',
  'compare.associate': 'Associer',
  'compare.compareErpPrices': 'Comparer avec les prix ERP',
  'compare.priceCompare.title': 'Comparaison de Prix entre Factures',
  'compare.differentSuppliers': 'Fournisseurs différents',
  'compare.sameSupplierRequired': 'Les deux factures doivent avoir le même fournisseur pour être comparées.',
  'compare.comparePrices': 'Comparer les Prix',
  'compare.invoices': 'Factures',
  'compare.status.linked': 'Associé',
  'compare.status.unlinked': 'Non associé',
  'compare.reparseInvoice': 'Reparser Facture',
  'compare.associatedDeliveries': 'Bons de Livraison Associés',
  'compare.compareInvoiceVsBl': 'Comparer facture vs total BL',
  'compare.allBlCompareStock': 'Tous les BL: Comparer + Stock',
  'compare.allBlStockCorrection': 'Tous les BL: Correction stock',
  'compare.unlink': 'Dissocier',
  'compare.reparseBl': 'Reparser BL',
  'compare.addDelivery': 'Ajouter un Bon de Livraison',
  'compare.noExtraDeliveries': 'Aucun bon de livraison supplémentaire disponible pour l\'association (même fournisseur requis).',
  'compare.deliveries': 'Bons de livraison',
  'compare.empty.deliveries': 'Aucun bon de livraison dans le jeu chargé.',
  'compare.otherDocuments': 'Autres documents',
  'compare.reparse': 'Reparser',
  'compare.detailsComparison': 'Comparaison de Détails',
  'compare.errors': 'ERREURS',
  'compare.excel': 'Excel',
  'compare.col.product': 'Produit',
  'compare.col.invoiceQty': 'Qté Facture',
  'compare.col.deliveryQty': 'Qté BL',
  'compare.col.actualQty': 'Qté Réelle',
  'compare.actualQtyLabel': 'Quantité réelle',
  'compare.col.qtyDiff': 'Différence Qté',
  'compare.col.currentUnitPrice': 'Prix Unit. Facture Actuelle',
  'compare.col.previousUnitPrice': 'Prix Unit. Facture Précédente',
  'compare.col.priceDiff': 'Différence Prix',
  'compare.col.code': 'Code',
  'compare.col.unit': 'Unité',
  'compare.col.totalInvoice': 'Total (Fact)',
  'compare.col.status': 'Statut',
  'compare.erpPrice.title': 'Comparaison avec les prix ERP',
  'compare.erpPrice.loading': 'Interrogation du web service ERP en cours…',
  'compare.col.productCode': 'Code produit',
  'compare.col.designation': 'Désignation',
  'compare.col.invoicePrice': 'Prix facture',
  'compare.col.erpPrice': 'Prix ERP',
  'compare.col.delta': 'Delta',
  'compare.label.number': 'Numéro',
  'compare.label.supplier': 'Fournisseur',
  'compare.label.client': 'Client',
  'compare.label.date': 'Date',
  'compare.label.invoice': 'Facture',
  'compare.noNumber': 'Sans numéro',
  'compare.snack.selectInvoiceAndBl': 'Veuillez sélectionner une facture et un BL',
  'compare.snack.linkSuccess': 'Relation créée avec succès',
  'compare.snack.linkError': 'Erreur lors de la création de la relation',
  'compare.snack.comparisonDone': 'Comparaison effectuée',
  'compare.snack.comparisonError': 'Erreur lors de la comparaison',
  'compare.snack.globalComparisonDone': 'Comparaison globale facture vs total BL effectuée',
  'compare.snack.selectTwoInvoices': 'Veuillez sélectionner deux factures',
  'compare.snack.priceComparisonDone': 'Comparaison de prix effectuée',
  'compare.snack.erpComparisonDone': 'Comparaison avec les prix ERP effectuée',
  'compare.snack.stockUpdated': 'Stock mis à jour (quantités livrées)',
  'compare.snack.stockCorrected': 'Stock mis à jour avec les quantités corrigées',
  'compare.snack.stockSkippedDiffs': 'Différences détectées: stock non mis à jour',
  'compare.snack.noLinkedBl': 'Aucun BL associé à cette facture',
  'compare.snack.reparseSuccess': 'Document re-parsé avec succès',
  'compare.snack.reparseError': 'Erreur lors du re-parsing',
  'compare.snack.excelDownloaded': 'Export Excel téléchargé',
  'compare.confirm.unlink': 'Confirmer la dissociation ?',

  // stock
  'stock.title': 'Gestion du Stock',
  'stock.subtitle': 'Consultation du stock mis à jour depuis les bons de livraison',
  'stock.placeholder': 'Rechercher un produit ou un code...',
  'stock.search': 'Rechercher',
  'stock.stat.productCount': 'Total de produits',
  'stock.stat.totalQty': 'Total des quantités',
  'stock.sortByCode': 'Trier par : Code',
  'stock.empty': 'Aucun produit en stock',
  'stock.productCount': '({count} produit)',
  'stock.productCountPlural': '({count} produits)',
  'stock.totalQuantity': 'Quantité totale:',
  'stock.col.code': 'Code',
  'stock.col.label': 'Libellé',
  'stock.col.quantity': 'Quantité',
  'stock.col.unit': 'Unité',
  'stock.col.lastUpdated': 'Dernière mise à jour',
  'stock.negativeTooltip': 'Stock négatif',
  'stock.unspecifiedSupplier': 'Non spécifié',
  'stock.snack.loadError': 'Erreur lors du chargement du stock',
  'stock.tab.onHand': 'Stock actuel',
  'stock.tab.movements': 'Mouvements',
  'stock.adjust': 'Ajuster le stock',
  'stock.movements.empty': 'Aucun mouvement de stock',
  'stock.movements.loadError': 'Erreur lors du chargement des mouvements',
  'stock.movements.col.date': 'Date',
  'stock.movements.col.type': 'Type',
  'stock.movements.col.code': 'Code',
  'stock.movements.col.qty': 'Quantité',
  'stock.movements.col.reason': 'Motif',
  'stock.movements.col.ref': 'Référence',
  'stock.movements.col.by': 'Par',
  'stock.adjust.title': 'Mouvement de stock manuel',
  'stock.adjust.productKey': 'Code produit',
  'stock.adjust.type': 'Type de mouvement',
  'stock.adjust.quantity': 'Quantité',
  'stock.adjust.reason': 'Motif',
  'stock.adjust.reference': 'Référence',
  'stock.adjust.submit': 'Enregistrer',
  'stock.adjust.cancel': 'Annuler',
  'stock.adjust.success': 'Mouvement enregistré',
  'stock.adjust.error': 'Erreur lors de l\'enregistrement du mouvement',
  'stock.type.In': 'Entrée',
  'stock.type.Out': 'Sortie',
  'stock.type.Adjustment': 'Ajustement',
  'stock.type.Transfer': 'Transfert',
  'stock.filter.movements': 'Filtrer les mouvements (code)...',

  // erp products
  'erpProducts.title': 'Produits ERP',
  'erpProducts.subtitle': 'Consultation des fiches locales et synchronisation unitaire avec le webservice',
  'erpProducts.enrichFromErp': 'Enrichir depuis ERP',
  'erpProducts.syncing': 'Sync en cours…',
  'erpProducts.changes': 'Changements',
  'erpProducts.refresh': 'Actualiser',
  'erpProducts.scope': 'Périmètre : {label}',
  'erpProducts.cancel': 'Annuler',
  'erpProducts.stat.total': 'Total',
  'erpProducts.stat.page': 'Page',
  'erpProducts.searchPlaceholder': 'Nom, réf, EAN, ID ERP, marque…',
  'erpProducts.filter.allBrands': '— Toutes marques —',
  'erpProducts.filter.mainCategory': '— Catégorie mère —',
  'erpProducts.filter.subCategory': '— Sous-catégorie —',
  'erpProducts.filter.subSubCategory': '— Sous-sous-catégorie —',
  'erpProducts.filter.allSources': 'Toutes sources',
  'erpProducts.filter.sourceExcel': 'Excel',
  'erpProducts.filter.sourceMerged': 'Excel + ERP',
  'erpProducts.filter.sourceErp': 'ERP seul',
  'erpProducts.filter': 'Filtrer',
  'erpProducts.syncFiltered': 'Sync filtrés',
  'erpProducts.reset': 'Réinitialiser',
  'erpProducts.loading': 'Chargement…',
  'erpProducts.empty': 'Aucun produit trouvé',
  'erpProducts.importExcel': 'Importer depuis Excel',
  'erpProducts.col.product': 'Produit',
  'erpProducts.col.refEan': 'Réf / EAN',
  'erpProducts.col.brand': 'Marque',
  'erpProducts.col.price': 'Prix',
  'erpProducts.col.stock': 'Stock',
  'erpProducts.col.source': 'Source',
  'erpProducts.col.sync': 'Sync',
  'erpProducts.tooltip.syncErp': "Synchroniser avec l'ERP",
  'erpProducts.pageInfo': 'Page {page} / {totalPages}',
  'erpProducts.close': 'Fermer',
  'erpProducts.syncErp': 'Sync ERP',
  'erpProducts.syncingShort': 'Sync…',
  'erpProducts.detail.erpId': 'ID ERP',
  'erpProducts.detail.reference': 'Référence',
  'erpProducts.detail.ean': 'EAN',
  'erpProducts.detail.brand': 'Marque',
  'erpProducts.detail.salePrice': 'Prix vente',
  'erpProducts.detail.purchasePrice': 'Prix achat',
  'erpProducts.detail.stock': 'Stock',
  'erpProducts.detail.vat': 'TVA %',
  'erpProducts.detail.category': 'Catégorie',
  'erpProducts.detail.name2': 'Nom 2',
  'erpProducts.detail.source': 'Source',
  'erpProducts.detail.excelFile': 'Fichier Excel',
  'erpProducts.detail.lastSync': 'Dernière sync',
  'erpProducts.detail.updated': 'Mis à jour',
  'erpProducts.detail.comment': 'Commentaire',
  'erpProducts.progress.fullCatalog': 'Sync catalogue ERP complet',
  'erpProducts.progress.filtered': 'Sync produits filtrés',
  'erpProducts.progress.enrich': 'Enrichissement ERP (produits locaux)',
  'erpProducts.snack.loadError': 'Erreur chargement produits',
  'erpProducts.snack.enrichStarted': 'Enrichissement ERP démarré…',
  'erpProducts.snack.syncCancelled': 'Sync annulée',
  'erpProducts.snack.syncFailed': 'Échec du démarrage de la synchronisation ERP',
  'erpProducts.snack.productSyncOk': 'Sync OK — {name}',

  // erp changes
  'erpChanges.title': 'Changements ERP',
  'erpChanges.subtitle': 'Suivi des modifications détectées sur les produits du webservice EuroBrico',
  'erpChanges.enrichFromErp': 'Enrichir depuis ERP',
  'erpChanges.syncing': 'Sync en cours…',
  'erpChanges.importExcel': 'Importer Excel',
  'erpChanges.importing': 'Import…',
  'erpChanges.excelPlusSync': 'Excel + Sync ERP',
  'erpChanges.refresh': 'Actualiser',
  'erpChanges.progressTitle': 'Enrichissement ERP en cours',
  'erpChanges.stat.filteredTotal': 'Total filtrés',
  'erpChanges.stat.unreadPage': 'Non lus (page)',
  'erpChanges.stat.lastSync': 'Dernière sync',
  'erpChanges.filter.unreadOnly': 'Non lus uniquement',
  'erpChanges.filter.allTypes': 'Tous les types',
  'erpChanges.filter.created': 'Création',
  'erpChanges.filter.updated': 'Modification',
  'erpChanges.filter.price': 'Prix',
  'erpChanges.filter.stock': 'Stock',
  'erpChanges.filter.deleted': 'Suppression',
  'erpChanges.filter.allValues': 'Toutes les valeurs',
  'erpChanges.filter.bothValues': 'Avant et Après renseignés',
  'erpChanges.filter.cleared': 'Valeur vidée (→ —)',
  'erpChanges.filter.added': 'Valeur ajoutée (— →)',
  'erpChanges.searchPlaceholder': 'Rechercher produit, réf, EAN, valeur…',
  'erpChanges.markSelected': 'Marquer sélection',
  'erpChanges.deleteSelected': 'Supprimer sélection',
  'erpChanges.markAllPage': 'Tout marquer (page)',
  'erpChanges.cleanupFalsePositives': 'Nettoyer faux positifs prix',
  'erpChanges.cleaning': 'Nettoyage…',
  'erpChanges.reset': 'Réinitialiser',
  'erpChanges.loading': 'Chargement des changements…',
  'erpChanges.empty': 'Aucun changement ERP pour ces filtres',
  'erpChanges.startSync': 'Lancer une synchronisation',
  'erpChanges.selectAll': 'Tout sélectionner',
  'erpChanges.col.date': 'Date',
  'erpChanges.col.type': 'Type',
  'erpChanges.col.product': 'Produit',
  'erpChanges.col.field': 'Champ',
  'erpChanges.col.before': 'Avant',
  'erpChanges.col.after': 'Après',
  'erpChanges.col.status': 'Statut',
  'erpChanges.status.read': 'Lu',
  'erpChanges.status.unread': 'Non lu',
  'erpChanges.pageInfo': 'Page {page} / {totalPages}',
  'erpChanges.syncLogsTitle': 'Dernières synchronisations',
  'erpChanges.col.started': 'Démarrée',
  'erpChanges.col.new': 'Nouveaux',
  'erpChanges.col.updated': 'Mis à jour',
  'erpChanges.col.failed': 'Échecs',
  'erpChanges.col.changes': 'Changements',
  'erpChanges.snack.loadError': 'Erreur lors du chargement des changements ERP',
  'erpChanges.snack.selectAtLeastOne': 'Sélectionnez au moins un changement',
  'erpChanges.snack.markedRead': '{count} changement(s) marqué(s) comme lu(s)',
  'erpChanges.snack.deleted': '{count} changement(s) supprimé(s)',
  'erpChanges.snack.enrichStarted': 'Enrichissement ERP démarré…',
  'erpChanges.snack.importExcel': 'Import Excel en cours…',
  'erpChanges.snack.importExcelSync': 'Import Excel + sync ERP…',
  'erpChanges.snack.importFailed': "Échec de l'import Excel",

  'common.save': 'Enregistrer',
  'common.saving': 'Enregistrement…',
  'common.edit': 'Modifier',
  'common.delete': 'Supprimer',
  'common.actions': 'Actions',
  'common.status': 'Statut',
  'common.date': 'Date',
  'common.notes': 'Notes',
  'common.name': 'Nom',
  'common.code': 'Code',
  'common.email': 'Email',
  'common.phone': 'Téléphone',
  'common.city': 'Ville',
  'common.address': 'Adresse',
  'common.postalCode': 'Code postal',
  'common.country': 'Pays',
  'common.ht': 'HT',
  'common.vat': 'TVA',
  'common.ttc': 'TTC',
  'common.qty': 'Qté',
  'common.description': 'Description',
  'common.ref': 'Réf',
  'common.totalHt': 'Total HT',
  'common.totalVat': 'Total TVA',
  'common.totalTtc': 'Total TTC',
  'common.unitPriceHt': 'Prix U. HT',
  'common.vatPercent': 'TVA %',
  'common.noLines': 'Aucune ligne.',
  'common.customer': 'Client',
  'common.supplier': 'Fournisseur',
  'common.active': 'Actif',
  'common.inactive': 'Inactif',
  'common.amountHt': 'Montant HT',
  'common.amountTtc': 'Montant TTC',
  'common.error': 'Erreur.',
  'common.detail': 'Détail',
  'common.none': '— aucune —',
  'common.optionalNone': '-- Aucune --',
  'common.notProvided': 'non renseigné',
  'common.noNumber': 'sans numéro',
  'common.select': '-- Sélectionner --',
  'common.addLine': '+ Ajouter une ligne',
  'common.addLineShort': '+ Ligne',
  'common.updated': 'mis à jour',
  'common.created': 'créé',
  'common.label': 'Libellé',

  // sales
  'sales.title': 'Gestion des Ventes & Clients',
  'sales.subtitle': 'Devis → Commande → Facture → Paiement / Avoir',
  'sales.searchPlaceholder': 'Rechercher (Client, Ref, N°)...',
  'sales.btn.newInvoice': 'Nouvelle Facture',
  'sales.btn.newOrder': 'Nouvelle Commande',
  'sales.btn.newQuote': 'Nouveau Devis',
  'sales.btn.newDeliveryNote': 'Nouveau BL',
  'sales.btn.newCustomer': 'Nouveau Client',
  'sales.tab.invoices': 'Factures Clients',
  'sales.tab.orders': 'Commandes Clients',
  'sales.tab.quotes': 'Devis',
  'sales.tab.creditNotes': 'Avoirs Clients',
  'sales.tab.deliveryNotes': 'Bons de Livraison',
  'sales.tab.customers': 'Répertoire Clients',
  'sales.col.invoiceNumber': 'N° Facture',
  'sales.col.orderNumber': 'N° Commande',
  'sales.col.quoteNumber': 'N° Devis',
  'sales.col.creditNoteNumber': 'N° Avoir',
  'sales.col.deliveryNumber': 'N° BL',
  'sales.col.dueDate': 'Échéance',
  'sales.col.paid': 'Payé',
  'sales.col.expiration': 'Expiration',
  'sales.col.linkedInvoice': 'Facture liée',
  'sales.col.order': 'Commande',
  'sales.col.vatNumber': 'TVA',
  'sales.col.balance': 'Solde',
  'sales.col.delivered': 'Livré',
  'sales.col.ordered': 'Commandé',
  'sales.col.orderedQty': 'Qté cmdée',
  'sales.col.deliveredQty': 'Qté livrée',
  'sales.col.codeRef': 'Code / Réf',
  'sales.col.linkedOrder': 'Commande liée',
  'sales.btn.pay': 'Payer',
  'sales.btn.createCreditNote': 'Créer avoir',
  'sales.btn.createDeliveryNote': 'Créer BL',
  'sales.btn.invoice': 'Facturer',
  'sales.btn.order': 'Commander',
  'sales.btn.validate': 'Valider',
  'sales.btn.apply': 'Appliquer',
  'sales.btn.createInvoice': 'Créer une facture',
  'sales.btn.createOrder': 'Créer une commande',
  'sales.btn.createQuote': 'Créer un devis',
  'sales.btn.createDeliveryNoteLink': 'Créer un BL',
  'sales.btn.createCustomer': 'Créer un client',
  'sales.btn.newCustomerLink': '+ Nouveau client',
  'sales.btn.validatePayment': 'Valider le Paiement',
  'sales.btn.createCustomerSubmit': 'Créer le client',
  'sales.empty.invoices': 'Aucune facture.',
  'sales.empty.orders': 'Aucune commande.',
  'sales.empty.quotes': 'Aucun devis.',
  'sales.empty.creditNotes': 'Aucun avoir client trouvé.',
  'sales.empty.deliveryNotes': 'Aucun bon de livraison.',
  'sales.empty.customers': 'Aucun client.',
  'sales.customerHash': 'Client #{id}',
  'sales.label.customer': 'Client:',
  'sales.label.date': 'Date:',
  'sales.label.status': 'Statut:',
  'sales.label.paid': 'Payé:',
  'sales.label.ht': 'HT:',
  'sales.label.vat': 'TVA:',
  'sales.label.ttc': 'TTC:',
  'sales.label.delivered': 'Livré: {qty}',
  'sales.label.ordered': 'Commandé: {qty}',
  'sales.selectCustomer': '-- Sélectionner un client --',
  'sales.notesPlaceholder': 'Notes internes / conditions',
  'sales.modal.editCustomer': 'Modifier le client',
  'sales.modal.newCustomer': 'Nouveau Client',
  'sales.modal.payment': 'Enregistrer un Règlement',
  'sales.modal.newDeliveryNote': 'Nouveau Bon de Livraison',
  'sales.modal.newQuote': 'Nouveau Devis',
  'sales.modal.newOrder': 'Nouvelle Commande',
  'sales.modal.newInvoice': 'Nouvelle Facture',
  'sales.customer.codeAuto': 'Code (auto si vide)',
  'sales.customer.codePlaceholder': 'CUST-...',
  'sales.customer.nameRequired': 'Nom *',
  'sales.customer.namePlaceholder': 'Raison sociale / nom',
  'sales.customer.vatNumber': 'N° TVA',
  'sales.customer.required': 'Client *',
  'sales.payment.invoiceNumber': 'Facture N°',
  'sales.payment.amount': 'Montant à régler :',
  'sales.payment.method': 'Mode de Règlement :',
  'sales.payment.cash': 'Espèces',
  'sales.payment.card': 'Carte Bancaire',
  'sales.payment.transfer': 'Virement',
  'sales.lines': 'Lignes',
  'sales.needCustomerFirst': "Créez d'abord un client dans l'onglet Répertoire Clients.",
  'sales.pdfDownloaded': 'PDF téléchargé: {fileName}',
  'sales.pdfError': 'Téléchargement PDF impossible.',
  'sales.selectCustomerError': 'Sélectionnez un client.',
  'sales.addLineError': 'Ajoutez au moins une ligne.',
  'sales.quoteCreated': 'Devis {number} créé.',
  'sales.orderCreated': 'Commande {number} créée.',
  'sales.invoiceCreated': 'Facture {number} créée.',
  'sales.action.createQuote': 'création du devis',
  'sales.action.createOrder': 'création de la commande',
  'sales.action.createInvoice': 'création de la facture',
  'sales.confirm.deleteCustomer': 'Supprimer le client « {name} » ?',
  'sales.customerDeleted': 'Client {name} supprimé.',
  'sales.customerDeleteError': 'Impossible de supprimer ce client.',
  'sales.customerNameRequired': 'Le nom du client est obligatoire.',
  'sales.customerSaved': 'Client {name} ({code}) {verb}.',
  'sales.customerSaveError': 'Erreur lors de {action} du client.',
  'sales.customerUpdateAction': 'la mise à jour',
  'sales.customerCreateAction': 'la création',
  'sales.orderFromQuote': 'Commande {order} créée depuis le devis {quote}.',
  'sales.quoteToOrderError': 'Conversion devis → commande impossible.',
  'sales.invoiceFromOrder': 'Facture {invoice} créée depuis la commande {order}.',
  'sales.orderToInvoiceError': 'Conversion commande → facture impossible.',
  'sales.paymentSaved': 'Paiement de {amount} € enregistré.',
  'sales.paymentError': 'Paiement impossible.',
  'sales.creditNoteFromInvoice': 'Avoir {creditNote} créé depuis la facture {invoice}.',
  'sales.creditNoteCreateError': "Erreur lors de la création de l'avoir.",
  'sales.creditNoteValidated': 'Avoir {number} validé.',
  'sales.creditNoteValidateError': "Erreur lors de la validation de l'avoir.",
  'sales.creditNoteApplied': 'Avoir {number} appliqué.',
  'sales.creditNoteApplyError': "Erreur lors de l'application de l'avoir.",
  'sales.detailLoadError': 'Impossible de charger le détail.',
  'sales.detailTitle.quote': 'Devis {number}',
  'sales.detailTitle.order': 'Commande {number}',
  'sales.detailTitle.invoice': 'Facture {number}',
  'sales.detailTitle.creditNote': 'Avoir {number}',
  'sales.detailTitle.deliveryNote': 'BL {number}',
  'sales.errorDuring': 'Erreur lors de la {action}.',
  'sales.deliveryNoteCreated': 'BL {number} créé.',
  'sales.deliveryNoteCreateError': 'Erreur lors de la création du BL.',
  'sales.deliveryNoteFromOrder': 'BL {delivery} créé depuis commande {order}.',
  'sales.invoiceFromDeliveryNote': 'Facture {invoice} créée depuis BL {delivery}.',
  'sales.genericError': 'Erreur.',
  'sales.pdfGenericError': 'Erreur PDF.',
  'sales.confirm.deleteDeliveryNote': 'Supprimer le BL {number} ?',
  'sales.deliveryNoteDeleted': 'BL {number} supprimé.',
  'sales.deleteError': 'Erreur suppression.',

  // purchases
  'purchases.title': 'Achats & Fournisseurs',
  'purchases.subtitle': 'Suivi des commandes fournisseurs et génération des factures fournisseurs depuis les documents parsés.',
  'purchases.searchPlaceholder': 'Rechercher (fournisseur, numéro, note)...',
  'purchases.btn.parsedDocuments': 'Documents parsés',
  'purchases.btn.uploadDocument': 'Uploader un document',
  'purchases.btn.newInvoice': 'Nouvelle facture',
  'purchases.btn.newOrder': 'Nouvelle commande',
  'purchases.btn.newSupplier': 'Nouveau fournisseur',
  'purchases.tab.supplierInvoices': 'Factures fournisseurs',
  'purchases.tab.purchaseOrders': 'Commandes fournisseurs',
  'purchases.tab.parsedDocuments': 'Documents parsés',
  'purchases.tab.receipts': 'Réceptions',
  'purchases.tab.suppliers': 'Répertoire Fournisseurs',
  'purchases.col.invoiceNumber': 'N° Facture',
  'purchases.col.document': 'Document',
  'purchases.col.order': 'Commande',
  'purchases.col.dueDate': 'Échéance',
  'purchases.col.orderNumber': 'N° Commande',
  'purchases.col.expectedDelivery': 'Livraison prévue',
  'purchases.col.received': 'Reçu',
  'purchases.col.documentNumber': 'N° document',
  'purchases.col.type': 'Type',
  'purchases.col.file': 'Fichier',
  'purchases.col.documentDate': 'Date document',
  'purchases.col.addedAt': 'Ajouté le',
  'purchases.col.target': 'Cible',
  'purchases.col.receiptNumber': 'N° réception',
  'purchases.col.sourceDocument': 'Document source',
  'purchases.col.cfa': 'CFA',
  'purchases.col.lines': 'Lignes',
  'purchases.col.qtyReceived': 'Qté reçue',
  'purchases.col.vatNumber': 'TVA',
  'purchases.btn.linkDocument': 'Lier document',
  'purchases.btn.matchOrder': 'Rapprocher commande',
  'purchases.btn.receiveDelivery': 'Réceptionner BL',
  'purchases.btn.comptabiliser': 'Comptabiliser',
  'purchases.btn.linkDocumentSubmit': 'Lier le document',
  'purchases.btn.match': 'Rapprocher',
  'purchases.btn.applyDelivery': 'Appliquer le bon de livraison',
  'purchases.btn.createSupplier': 'Créer le fournisseur',
  'purchases.btn.newSupplierLink': '+ Nouveau fournisseur',
  'purchases.btn.saveInvoice': 'Enregistrer la facture',
  'purchases.btn.saveOrder': 'Enregistrer la commande',
  'purchases.empty.supplierInvoices': 'Aucune facture fournisseur trouvée.',
  'purchases.empty.purchaseOrders': 'Aucune commande fournisseur trouvée.',
  'purchases.empty.parsedDocuments': 'Aucun document parsé (facture ou BL).',
  'purchases.empty.receipts': 'Aucune réception. Comptabilisez un document BL.',
  'purchases.empty.suppliers': "Aucun fournisseur. Créez-en un pour démarrer le cycle d'achat.",
  'purchases.hint.parsedDocuments': 'Documents OCR (Documents). Comptabiliser crée une facture fournisseur ou une réception (ErpReceipts).',
  'purchases.hint.receipts': 'Réceptions métier (ErpReceipts) créées après comptabilisation d’un BL parsé.',
  'purchases.status.comptabilise': 'Comptabilisé',
  'purchases.status.pending': 'En attente',
  'purchases.type.invoice': 'Facture',
  'purchases.type.deliveryNote': 'BonLivraison',
  'purchases.target.invoiceFo': 'Facture FO',
  'purchases.target.receipt': 'Réception',
  'purchases.docHash': 'Doc #{id}',
  'purchases.supplierHash': '#{id}',
  'purchases.label.supplier': 'Fournisseur:',
  'purchases.label.date': 'Date:',
  'purchases.label.status': 'Statut:',
  'purchases.label.document': 'Document:',
  'purchases.label.order': 'Commande:',
  'purchases.label.expectedDelivery': 'Livraison prévue:',
  'purchases.parsedHintPrefix': 'Documents OCR (',
  'purchases.parsedHintMid': '). ',
  'purchases.parsedHintComptabiliser': 'Comptabiliser',
  'purchases.parsedHintCreates': ' crée une ',
  'purchases.parsedHintOr': ' ou une ',
  'purchases.modal.comptabiliserFromDoc': 'Comptabiliser une facture depuis un document',
  'purchases.modal.newSupplierInvoice': 'Nouvelle facture fournisseur',
  'purchases.modal.linkDocument': 'Lier un document à la facture fournisseur',
  'purchases.modal.newPurchaseOrder': 'Nouvelle commande fournisseur',
  'purchases.modal.editSupplier': 'Modifier le fournisseur',
  'purchases.modal.newSupplier': 'Nouveau fournisseur',
  'purchases.modal.matchOrder': 'Rapprocher la facture fournisseur à une commande',
  'purchases.modal.receiveDelivery': 'Réceptionner un bon de livraison',
  'purchases.modal.comptabiliserDoc': 'Comptabiliser le document',
  'purchases.selectSupplier': '-- Sélectionner un fournisseur --',
  'purchases.selectDocument': '-- Sélectionner un document --',
  'purchases.selectPurchaseOrder': '-- Sélectionner une commande fournisseur --',
  'purchases.selectDeliveryNote': '-- Sélectionner un bon de livraison --',
  'purchases.defaultVat': 'TVA par défaut (%)',
  'purchases.detectedSupplier': 'Fournisseur détecté sur le document:',
  'purchases.comptabilising': 'Comptabilisation…',
  'purchases.number': 'Numéro',
  'purchases.numberPlaceholder': 'Laisser vide pour auto-numérotation',
  'purchases.notesPlaceholder': 'Commentaire optionnel',
  'purchases.invoiceLines': 'Lignes de facture',
  'purchases.orderLines': 'Lignes de commande',
  'purchases.invoiceLabel': 'Facture:',
  'purchases.orderLabel': 'Commande:',
  'purchases.match.balanced': 'Rapprochement équilibré',
  'purchases.match.gaps': 'Écarts détectés',
  'purchases.match.invoiceHt': 'Total HT facture:',
  'purchases.match.orderHt': 'Total HT commande:',
  'purchases.match.deltaHt': 'Delta HT:',
  'purchases.match.matchedLines': 'Lignes appariées:',
  'purchases.match.qtyGaps': 'Écarts quantité:',
  'purchases.match.priceGaps': 'Écarts prix:',
  'purchases.match.moreWarnings': '{count} autre(s) écart(s) non affiché(s).',
  'purchases.supplier.codeAuto': 'Code (auto si vide)',
  'purchases.supplier.codePlaceholder': 'SUP-...',
  'purchases.supplier.nameRequired': 'Nom *',
  'purchases.supplier.namePlaceholder': 'Raison sociale',
  'purchases.supplier.vatNumber': 'N° TVA',
  'purchases.supplier.required': 'Fournisseur *',
  'purchases.purchaseOrderOptional': 'Commande fournisseur (optionnel)',
  'purchases.parsedDocument': 'Document parsé',
  'purchases.deliveryDocument': 'Bon de livraison parsé',
  'purchases.purchaseOrder': 'Commande fournisseur',
  'purchases.hint.invoiceCreates': 'Crée une facture fournisseur comptabilisée (statut Validated).',
  'purchases.hint.deliveryCreates': 'Crée une réception (ErpReceipts) et met à jour le stock.',
  'purchases.autoCreated': 'Facture fournisseur #{id} créée automatiquement depuis le parsing.',
  'purchases.needSupplierFirst': "Créez d'abord un fournisseur.",
  'purchases.pdfDownloaded': 'PDF téléchargé: {fileName}',
  'purchases.pdfError': 'Téléchargement PDF impossible.',
  'purchases.confirm.deleteSupplier': 'Supprimer le fournisseur « {name} » ?',
  'purchases.supplierDeleted': 'Fournisseur {name} supprimé.',
  'purchases.selectSupplierError': 'Veuillez sélectionner un fournisseur.',
  'purchases.selectSupplierAndDoc': 'Veuillez sélectionner un fournisseur et un document.',
  'purchases.invoiceComptabilised': 'Facture comptabilisée → {number}.{warnings}',
  'purchases.blComptabilised': 'BL comptabilisé → réception {number} (ErpReceipts).',
  'purchases.stockAlreadyFed': 'Stock déjà alimenté (pas de double entrée).',
  'purchases.invoiceFromDoc': 'Facture {number} comptabilisée depuis le document.',
  'purchases.supplierInvoiceCreated': 'Facture fournisseur {number} créée.',
  'purchases.supplierInvoiceCreateError': 'Erreur lors de la création de la facture fournisseur.',
  'purchases.purchaseOrderCreated': 'Commande fournisseur {number} créée.',
  'purchases.purchaseOrderCreateError': 'Erreur lors de la création de la commande fournisseur.',
  'purchases.supplierSaved': 'Fournisseur {name} ({code}) {verb}.',
  'purchases.supplierSaveError': 'Erreur lors de {action} du fournisseur.',
  'purchases.supplierUpdateAction': 'la mise à jour',
  'purchases.supplierCreateAction': 'la création',
  'purchases.selectDocumentError': 'Veuillez sélectionner un document.',
  'purchases.documentLinked': 'Document lié à la facture fournisseur.',
  'purchases.selectDeliveryError': 'Veuillez sélectionner un bon de livraison.',
  'purchases.receiveDeliveryError': 'Erreur lors de la réception du bon de livraison.',
  'purchases.selectPurchaseOrderError': 'Veuillez sélectionner une commande fournisseur.',
  'purchases.matchPreviewError': 'Erreur lors de la prévisualisation du rapprochement.',
  'purchases.detailLoadError': 'Impossible de charger le détail.',
  'purchases.detailTitle.order': 'Commande {number}',
  'purchases.detailTitle.invoice': 'Facture {number}',
  'purchases.detailTitle.receipt': 'Réception {number}',
  'purchases.addLineError': 'Ajoutez au moins une ligne.',
  'purchases.supplierNameRequired': 'Le nom du fournisseur est obligatoire.',
  'purchases.documentMissing': 'Document manquant.',
  'purchases.comptabiliseError': 'Erreur lors de la comptabilisation.',
  'purchases.supplierDeleteError': 'Impossible de supprimer ce fournisseur.',
  'purchases.linkDocumentError': 'Erreur lors de la liaison du document.',
  'purchases.matchError': 'Erreur lors du rapprochement de la facture fournisseur.',
  'purchases.supplierNameHash': 'Fournisseur #{id}',
  'purchases.stockUpdated': 'Stock +{qty} ({count} mouvements).',
  'purchases.receiveStockEntry': 'Entrée stock: {qty} unité(s) sur {count} produit(s).',
  'purchases.receiveStockAlreadyFed': 'Stock déjà alimenté pour ce BL (pas de double entrée).',
  'purchases.matchOk': 'Rapprochement effectué entre la facture {invoice} et la commande {order}.',
  'purchases.matchWithGaps': 'Rapprochement effectué avec écarts entre la facture {invoice} et la commande {order}.',
  'purchases.receiveApplied': 'Réception BL appliquée sur la commande {order}.',

  // cash
  'cash.title': 'Module de Caisse & Finance',
  'cash.subtitle': 'Ouverture, opérations et clôture de la session de caisse.',
  'cash.btn.newOperation': 'Nouvelle Opération',
  'cash.btn.close': 'Clôturer la Caisse',
  'cash.btn.open': 'Ouvrir la Caisse',
  'cash.tab.active': 'Session active',
  'cash.tab.history': 'Historique',
  'cash.noSession.title': "Aucune session de caisse n'est ouverte actuellement.",
  'cash.noSession.hint': 'Veuillez ouvrir une session pour pouvoir enregistrer des ventes en caisse et effectuer des dépôts/retraits.',
  'cash.metric.sessionNumber': 'Session N°',
  'cash.metric.openingBalance': 'Fond de Caisse Initial',
  'cash.metric.theoretical': 'Solde théorique',
  'cash.metric.inOut': 'Entrées / Sorties',
  'cash.metric.openedBy': 'Ouvert par',
  'cash.metric.status': 'Statut',
  'cash.operationsTitle': 'Opérations de la Session',
  'cash.col.date': 'Date',
  'cash.col.operationType': "Type d'Opération",
  'cash.col.description': 'Description',
  'cash.col.reference': 'Référence',
  'cash.col.amount': 'Montant',
  'cash.col.author': 'Auteur',
  'cash.col.type': 'Type',
  'cash.col.number': 'N°',
  'cash.col.opening': 'Ouverture',
  'cash.col.status': 'Statut',
  'cash.col.float': 'Fond',
  'cash.col.closing': 'Clôture',
  'cash.col.variance': 'Écart',
  'cash.empty.operations': 'Aucune opération enregistrée dans cette session.',
  'cash.empty.sessions': 'Aucune session enregistrée.',
  'cash.empty.historyOps': 'Aucune opération.',
  'cash.historyTitle': 'Sessions récentes',
  'cash.detailTitle': 'Détail — {number}',
  'cash.openedAt': 'Ouvert: {date} par {by}',
  'cash.closedAt': 'Clôturé: {date} par {by}',
  'cash.expected': 'Attendu: {amount}',
  'cash.modal.open': 'Ouverture de Caisse',
  'cash.modal.close': 'Clôture de Caisse',
  'cash.modal.newOp': 'Nouvelle Opération de Caisse',
  'cash.openingBalanceLabel': "Fond de caisse d'ouverture (€) :",
  'cash.confirmOpen': "Confirmer l'ouverture",
  'cash.theoreticalLabel': 'Solde théorique:',
  'cash.realCountLabel': 'Comptage réel en caisse (€) :',
  'cash.varianceLabel': 'Écart:',
  'cash.opTypeLabel': "Type d'opération :",
  'cash.op.deposit': "Dépôt d'espèces",
  'cash.op.withdrawal': 'Retrait de caisse',
  'cash.op.salePayment': 'Vente directe comptant',
  'cash.amountLabel': 'Montant (€) :',
  'cash.descriptionLabel': 'Description :',
  'cash.descriptionPlaceholder': 'Motif ou référence',
  'cash.referenceLabel': 'Référence document :',
  'cash.referencePlaceholder': 'FAC-... / ticket',
  'cash.opLabel.deposit': 'Dépôt',
  'cash.opLabel.withdrawal': 'Retrait',
  'cash.opLabel.salePayment': 'Vente comptant',
  'cash.loadSessionError': 'Impossible de charger la session.',
  'cash.opened': 'Session de caisse ouverte.',
  'cash.openError': "Impossible d'ouvrir la caisse.",
  'cash.closed': 'Caisse clôturée. Écart: {diff} €.',
  'cash.closeError': 'Impossible de clôturer la caisse.',
  'cash.invalidAmount': 'Montant invalide.',
  'cash.opSaved': 'Opération enregistrée.',
  'cash.opSaveError': "Impossible d'enregistrer l'opération.",

  // numbering
  'numbering.title': 'Numérotation des documents',
  'numbering.subtitle': 'Préfixes, formats et compteurs chronologiques (devis, factures, avoirs, achats…).',
  'numbering.initDefaults': 'Initialiser les défauts',
  'numbering.placeholdersHint': 'Placeholders disponibles dans le format :',
  'numbering.example': 'Exemple :',
  'numbering.empty.title': 'Aucune séquence configurée',
  'numbering.empty.hint': 'Cliquez sur « Initialiser les défauts » pour créer les compteurs métier.',
  'numbering.prefix': 'Préfixe',
  'numbering.year': 'Année',
  'numbering.nextNumber': 'Prochain n°',
  'numbering.format': 'Format',
  'numbering.type.Quote': 'Devis',
  'numbering.type.Order': 'Commande client',
  'numbering.type.Invoice': 'Facture client',
  'numbering.type.CreditNote': 'Avoir client',
  'numbering.type.PurchaseOrder': 'Commande fournisseur',
  'numbering.type.SupplierInvoice': 'Facture fournisseur',
  'numbering.type.DeliveryNote': 'Bon de livraison',
  'numbering.loadError': 'Impossible de charger les séquences.',
  'numbering.defaultsReady': 'Séquences par défaut créées / vérifiées.',
  'numbering.initError': "Impossible d'initialiser les séquences.",
  'numbering.prefixRequired': 'Le préfixe est obligatoire.',
  'numbering.nextNumberInvalid': 'Le prochain numéro doit être ≥ 1.',
  'numbering.formatRequired': 'Le format est obligatoire.',
  'numbering.saved': 'Séquence {type} enregistrée.',
  'numbering.saveError': 'Enregistrement impossible.',

  // admin
  'admin.title': 'Administration',
  'admin.tab.tenants': 'Tenants',
  'admin.tab.companies': 'Sociétés',
  'admin.tab.roles': 'Rôles',
  'admin.tab.users': 'Utilisateurs',
  'admin.btn.newTenant': 'Nouveau Tenant',
  'admin.btn.newCompany': 'Nouvelle Société',
  'admin.btn.newRole': 'Nouveau Rôle',
  'admin.btn.newUser': 'Nouvel utilisateur',
  'admin.col.name': 'Nom',
  'admin.col.active': 'Actif',
  'admin.col.companies': 'Sociétés',
  'admin.col.createdAt': 'Créé le',
  'admin.col.createdAtF': 'Créée le',
  'admin.col.language': 'Langue',
  'admin.col.currency': 'Devise',
  'admin.col.permissions': 'Permissions',
  'admin.col.user': 'Utilisateur',
  'admin.col.email': 'Email',
  'admin.col.role': 'Rôle',
  'admin.col.activeCompany': 'Société active',
  'admin.col.company': 'Société',
  'admin.col.id': 'ID',
  'admin.empty.tenants': 'Aucun tenant.',
  'admin.empty.companies': 'Aucune société.',
  'admin.empty.companiesHint': 'Aucune société.',
  'admin.empty.createCompany': 'Créer une société',
  'admin.empty.roles': 'Aucun rôle.',
  'admin.empty.users': 'Aucun utilisateur.',
  'admin.empty.access': 'Aucun accès.',
  'admin.active': 'Actif',
  'admin.inactive': 'Inactif',
  'admin.activeF': 'Active',
  'admin.inactiveF': 'Inactive',
  'admin.role.admin': 'Admin',
  'admin.role.user': 'User',
  'admin.companiesAccessible': 'Sociétés accessibles :',
  'admin.modal.newRole': 'Nouveau rôle',
  'admin.modal.editRole': 'Modifier le rôle',
  'admin.modal.editTenant': 'Modifier le tenant',
  'admin.modal.newTenant': 'Nouveau Tenant',
  'admin.modal.editCompany': 'Modifier la société',
  'admin.modal.newCompany': 'Nouvelle Société',
  'admin.modal.editUser': 'Modifier utilisateur',
  'admin.modal.newUser': 'Nouvel utilisateur',
  'admin.modal.resetPassword': 'Réinitialiser le mot de passe',
  'admin.modal.assign': 'Assigner {username} à une société',
  'admin.role.nameRequired': 'Nom du rôle *',
  'admin.role.allPermissions': 'Toutes les permissions',
  'admin.role.adminLocked': 'Le rôle Admin a automatiquement toutes les permissions (non modifiable).',
  'admin.role.namePlaceholder': 'Ex: Comptable, Commercial…',
  'admin.permissions': 'Permissions',
  'admin.tenant.namePlaceholder': 'Nom du tenant',
  'admin.company.namePlaceholder': 'Nom de la société',
  'admin.company.tenantRequired': 'Tenant *',
  'admin.user.usernameRequired': "Nom d'utilisateur *",
  'admin.user.usernamePlaceholder': 'Login / email',
  'admin.user.emailPlaceholder': 'email@domaine.com',
  'admin.user.firstName': 'Prénom',
  'admin.user.lastName': 'Nom',
  'admin.user.lastNamePlaceholder': 'Nom de famille',
  'admin.user.passwordRequired': 'Mot de passe *',
  'admin.user.passwordOptional': 'Nouveau mot de passe (laisser vide = inchangé)',
  'admin.user.defaultCompany': 'Société par défaut',
  'admin.user.businessRole': 'Rôle métier',
  'admin.user.standardRole': '— Utilisateur standard —',
  'admin.user.isAdmin': 'Administrateur',
  'admin.user.label': 'Utilisateur :',
  'admin.user.newPassword': 'Nouveau mot de passe *',
  'admin.user.newPasswordPlaceholder': 'Nouveau mot de passe',
  'admin.resetting': 'Réinitialisation…',
  'admin.reset': 'Réinitialiser',
  'admin.assigning': 'Assignation…',
  'admin.assign': 'Assigner',
  'admin.nameRequired': 'Nom *',
  'admin.title.edit': 'Modifier',
  'admin.title.delete': 'Supprimer',
  'admin.title.editPermissions': 'Modifier / permissions',
  'admin.title.resetPassword': 'Réinitialiser mot de passe',
  'admin.title.assignCompany': 'Assigner société',
  'admin.title.remove': 'Retirer',
  'admin.error.nameRequired': 'Nom requis.',
  'admin.error.tenantRequired': 'Tenant requis.',
  'admin.error.usernameRequired': 'Username requis.',
  'admin.error.passwordRequired': 'Mot de passe requis.',
  'admin.error.roleNameRequired': 'Nom du rôle requis.',
  'admin.tenantSaved': 'Tenant "{name}" sauvegardé.',
  'admin.companySaved': 'Société "{name}" sauvegardée.',
  'admin.userAssigned': 'Utilisateur assigné à la société.',
  'admin.confirm.removeAccess': 'Retirer cet utilisateur de la société ?',
  'admin.accessRemoved': 'Accès retiré.',
  'admin.userUpdated': 'Utilisateur modifié.',
  'admin.userCreated': 'Utilisateur créé.',
  'admin.confirm.deleteUser': 'Supprimer l\'utilisateur "{username}" ?',
  'admin.userDeleted': 'Utilisateur "{username}" supprimé.',
  'admin.passwordReset': 'Mot de passe réinitialisé pour "{username}".',
  'admin.roleUpdated': 'Rôle modifié.',
  'admin.roleCreated': 'Rôle créé.',
  'admin.confirm.deleteRole': 'Supprimer le rôle "{name}" ?',
  'admin.roleDeleted': 'Rôle "{name}" supprimé.',
  'admin.perm.cat.sales': 'Ventes',
  'admin.perm.cat.purchases': 'Achats',
  'admin.perm.cat.stock': 'Stock',
  'admin.perm.cat.erp': 'Produits ERP',
  'admin.perm.cat.documents': 'Documents',
  'admin.perm.cat.cash': 'Caisse',
  'admin.perm.cat.settings': 'Paramètres',
  'admin.perm.cat.admin': 'Administration',
  'admin.perm.sec.customers': 'Clients',
  'admin.perm.sec.quotes': 'Devis',
  'admin.perm.sec.orders': 'Commandes',
  'admin.perm.sec.deliveryNotes': 'Bons de livraison',
  'admin.perm.sec.invoices': 'Factures',
  'admin.perm.sec.suppliers': 'Fournisseurs',
  'admin.perm.sec.purchaseOrders': 'Commandes achat',
  'admin.perm.sec.receipts': 'Réceptions',
  'admin.perm.sec.supplierInvoices': 'Factures fournisseur',
  'admin.perm.sec.stock': 'Stock',
  'admin.perm.sec.products': 'Catalogue produits',
  'admin.perm.sec.erpChanges': 'Changements ERP',
  'admin.perm.sec.documents': 'Documents',
  'admin.perm.sec.cash': 'Caisse',
  'admin.perm.sec.numbering': 'Numérotation',
  'admin.perm.sec.users': 'Utilisateurs',
  'admin.perm.sec.roles': 'Rôles',
  'admin.perm.action.Read': 'Lecture',
  'admin.perm.action.Create': 'Création',
  'admin.perm.action.Update': 'Modification',
  'admin.perm.action.Delete': 'Suppression',
  'admin.perm.action.Manage': 'Gestion',
  'admin.perm.action.Upload': 'Import',
  'admin.perm.action.Link': 'Association',

  // assistant
  'assistant.title': 'Assistant magasin',
  'assistant.subtitle': 'Conseils produits, devis et commande',
  'assistant.project': 'Projet',
  'assistant.budget': 'Budget',
  'assistant.cart': 'Panier',
  'assistant.cartEmpty': 'Aucun produit dans le panier.',
  'assistant.close': 'Fermer',
  'assistant.remove': 'Retirer',
  'assistant.welcome': 'Bonjour ! Je suis l’assistant magasin. Demandez un produit, une marque ou un projet (peinture, électricité…).',
  'assistant.redirecting': 'Redirection vers l’assistant magasin…',
  'assistant.placeholder': 'Ex. peinture blanche 10L, ampoule LED, perceuse…',
  'assistant.send': 'Envoyer',
  'assistant.quote': 'Demander un devis',
  'assistant.order': 'Commander',
  'assistant.downloadQuote': 'Télécharger le devis',
  'assistant.downloadInvoice': 'Télécharger la facture',
  'assistant.payCard': 'Payer par carte',
  'assistant.product': 'Produit',
  'assistant.price': 'Prix',
  'assistant.qty': 'Qté',
  'assistant.error': 'Désolé, une erreur est survenue. Réessayez.',
  'assistant.newProject': 'Nouveau projet',
  'assistant.photo': 'Joindre une photo',
  'assistant.listening': 'Écoute en cours…',
  'assistant.lang': 'Langue',
  'assistant.nextStep': 'Étape suivante',
  'assistant.next': 'Suivant',
  'assistant.reviewCart': 'Revue panier',
  'assistant.langSwitched': 'Langue : français.'
};

const NL: Dict = {
  'common.ok': 'OK',
  'common.close': 'Sluiten',
  'common.cancel': 'Annuleren',
  'common.reset': 'Resetten',
  'common.refresh': 'Vernieuwen',
  'common.loading': 'Laden…',
  'common.prev': 'Vorige',
  'common.next': 'Volgende',
  'common.search': 'Zoeken',
  'common.lang': 'Taal',
  'common.show': 'Tonen',
  'common.hide': 'Verbergen',

  'nav.brandSub': 'Industrial Logistics',
  'nav.newAnalysis': 'Nieuwe analyse',
  'nav.logout': 'Afmelden',
  'nav.upload': 'Upload',
  'nav.search': 'Zoeken',
  'nav.compare': 'Koppeling',
  'nav.stock': 'Voorraad',
  'nav.erpProducts': 'Producten',
  'nav.erpChanges': 'Wijzigingen',
  'nav.assistant': 'Assistent',
  'nav.assistantTab': 'Winkel',
  'nav.sales': 'Verkoop & Klanten',
  'nav.purchases': 'Aankopen',
  'nav.cash': 'Kassa',
  'nav.numbering': 'Nummering',
  'nav.admin': 'Beheer',
  'nav.title.upload': 'Documentbeheer',
  'nav.title.search': 'Zoeken',
  'nav.title.compare': 'Koppeling',
  'nav.title.stock': 'Documentbeheer',
  'nav.title.erpProducts': 'ERP-producten',
  'nav.title.erpChanges': 'ERP-wijzigingen',
  'nav.title.assistant': 'Winkelassistent',
  'nav.title.sales': 'Verkoop & Klanten',
  'nav.title.purchases': 'Aankopen',
  'nav.title.cash': 'Kassa',
  'nav.title.numbering': 'Nummering',
  'nav.title.admin': 'Beheer',
  'nav.title.default': 'Documentbeheer',

  'accessDenied.title': 'Geen toegang',
  'accessDenied.message': 'Uw account heeft niet de nodige rechten voor deze pagina.',
  'accessDenied.asUser': 'Aangemeld als',
  'accessDenied.goHome': 'Naar startpagina',
  'accessDenied.logout': 'Afmelden',

  'login.brandSub': 'Aanmelden',
  'login.title': 'Toegang tot de applicatie',
  'login.hint': 'Gebruik uw EuroBrico Backup-account',
  'login.email': 'E-mail',
  'login.password': 'Wachtwoord',
  'login.submit': 'Aanmelden',
  'login.submitting': 'Bezig…',
  'login.required': 'E-mail en wachtwoord verplicht',
  'login.invalid': 'Ongeldige gegevens',

  'upload.title': 'Documenten uploaden',
  'upload.newDocument': 'Nieuw document',
  'upload.dropPlaceholder': 'Sleep uw bestand hierheen',
  'upload.dropHint': 'Sleep uw bestand hierheen of klik om te bladeren',
  'upload.documentType': 'Documenttype',
  'upload.type.invoice': 'Factuur',
  'upload.type.deliveryNote': 'Leveringsbon',
  'upload.type.other': 'Andere',
  'upload.supplier': 'Leverancier',
  'upload.selectPlaceholder': '-- Selecteren --',
  'upload.noSuppliersHint': 'Geen leverancier geregistreerd. De leverancier wordt toegevoegd bij inspectie van het bestand.',
  'upload.number': 'Nummer',
  'upload.numberPlaceholder': 'Documentnummer',
  'upload.client': 'Klant',
  'upload.clientPlaceholder': 'Klantnaam',
  'upload.date': 'Datum',
  'upload.aiEnabled': 'AI-extractie geactiveerd',
  'upload.aiAccuracy': 'Nauwkeurigheid ~99%',
  'upload.submit': 'Uploaden',
  'upload.submitting': 'Bezig met uploaden...',
  'upload.reset': 'Resetten',
  'upload.recentHistory': 'Recente geschiedenis',
  'upload.viewAll': 'Alles zien',
  'upload.col.document': 'Document',
  'upload.col.supplier': 'Leverancier',
  'upload.col.status': 'Status',
  'upload.col.date': 'Datum',
  'upload.col.id': 'ID',
  'upload.col.type': 'Type',
  'upload.col.client': 'Klant',
  'upload.unidentified': 'Niet geïdentificeerd',
  'upload.status.validated': 'Gevalideerd',
  'upload.status.error': 'Fout',
  'upload.aiMetrics': 'AI-metrics',
  'upload.indexedDocuments': 'Geïndexeerde documenten',
  'upload.unlinkedTitle': 'Niet-gekoppelde documenten — {supplier}',
  'upload.download': 'Downloaden',
  'upload.relative.justNow': 'Zojuist',
  'upload.relative.hoursAgo': '{hours}u geleden',
  'upload.relative.daysAgo': '{days}d geleden',
  'upload.snack.selectFile': 'Selecteer een bestand',
  'upload.snack.success': 'Document succesvol geüpload',
  'upload.snack.duplicate': 'Dit document bestaat al in het systeem',
  'upload.snack.uploadError': 'Fout bij uploaden',
  'upload.snack.linkSuccess': 'LB gekoppeld aan factuur',
  'upload.snack.linkError': 'Fout bij koppelen',
  'upload.snack.invoicesFound': '{count} facturen gevonden. Doorverwijzing naar koppelpagina...',
  'upload.snack.noInvoice': 'Geen factuur met dit nummer. Alle facturen van de leverancier worden getoond...',
  'upload.confirm.linkInvoice': 'Factuur gevonden: {numero}\nKoppelen aan deze LB?',

  'search.title': 'Documenten zoeken',
  'search.subtitle': 'Zoeken op tekst, nummer, klant of leverancier',
  'search.placeholder': 'Zoek een document...',
  'search.submit': 'Zoeken',
  'search.stat.results': 'Resultaten',
  'search.stat.invoices': 'Facturen',
  'search.stat.deliveryNotes': 'Leveringsbonnen',
  'search.activeFilter': 'Actief filter: « {query} »',
  'search.loading': 'Bezig met zoeken…',
  'search.empty.welcome': 'Voer een term in en klik op Zoeken',
  'search.empty.hint': 'Nummer, klant, leverancier of documenttekst',
  'search.empty.none': 'Geen document gevonden',
  'search.resultsTitle': 'Documenten',
  'search.col.id': 'ID',
  'search.col.type': 'Type',
  'search.col.number': 'Nummer',
  'search.col.supplier': 'Leverancier',
  'search.col.client': 'Klant',
  'search.col.date': 'Datum',
  'search.col.actions': 'Acties',
  'search.type.invoice': 'Factuur',
  'search.type.bl': 'LB',
  'search.type.other': 'Andere',
  'search.tooltip.openAssociation': 'Openen in Koppeling',
  'search.tooltip.download': 'Downloaden',
  'search.error.load': 'Documenten laden mislukt. Controleer of de API draait.',

  'compare.title': 'Koppeling & vergelijking van documenten',
  'compare.subtitle': 'Geautomatiseerde reconciliatie van facturen en leveringsbonnen.',
  'compare.exportAll': 'Alles exporteren (Excel)',
  'compare.validateAssociation': 'Koppeling valideren',
  'compare.suggestedInvoices': 'Voorgestelde facturen voor LB #{blId}',
  'compare.selection.association': 'Huidige selectie (Factuur-LB)',
  'compare.associate': 'Koppelen',
  'compare.compareErpPrices': 'Vergelijken met ERP-prijzen',
  'compare.priceCompare.title': 'Prijsvergelijking tussen facturen',
  'compare.differentSuppliers': 'Verschillende leveranciers',
  'compare.sameSupplierRequired': 'Beide facturen moeten dezelfde leverancier hebben.',
  'compare.comparePrices': 'Prijzen vergelijken',
  'compare.invoices': 'Facturen',
  'compare.status.linked': 'Gekoppeld',
  'compare.status.unlinked': 'Niet gekoppeld',
  'compare.reparseInvoice': 'Factuur herparsen',
  'compare.associatedDeliveries': 'Gekoppelde leveringsbonnen',
  'compare.compareInvoiceVsBl': 'Factuur vs totaal LB vergelijken',
  'compare.allBlCompareStock': 'Alle LB: Vergelijken + Voorraad',
  'compare.allBlStockCorrection': 'Alle LB: Voorraadcorrectie',
  'compare.unlink': 'Ontkoppelen',
  'compare.reparseBl': 'LB herparsen',
  'compare.addDelivery': 'Leveringsbon toevoegen',
  'compare.noExtraDeliveries': 'Geen extra leveringsbon beschikbaar (zelfde leverancier vereist).',
  'compare.deliveries': 'Leveringsbonnen',
  'compare.empty.deliveries': 'Geen leveringsbon in de geladen set.',
  'compare.otherDocuments': 'Andere documenten',
  'compare.reparse': 'Herparsen',
  'compare.detailsComparison': 'Detailvergelijking',
  'compare.errors': 'FOUTEN',
  'compare.excel': 'Excel',
  'compare.col.product': 'Product',
  'compare.col.invoiceQty': 'Aantal factuur',
  'compare.col.deliveryQty': 'Aantal LB',
  'compare.col.actualQty': 'Werkelijk aantal',
  'compare.actualQtyLabel': 'Werkelijke hoeveelheid',
  'compare.col.qtyDiff': 'Verschil aantal',
  'compare.col.currentUnitPrice': 'Eenheidsprijs huidige factuur',
  'compare.col.previousUnitPrice': 'Eenheidsprijs vorige factuur',
  'compare.col.priceDiff': 'Prijsverschil',
  'compare.col.code': 'Code',
  'compare.col.unit': 'Eenheid',
  'compare.col.totalInvoice': 'Totaal (fact.)',
  'compare.col.status': 'Status',
  'compare.erpPrice.title': 'Vergelijking met ERP-prijzen',
  'compare.erpPrice.loading': 'ERP-webservice wordt bevraagd…',
  'compare.col.productCode': 'Productcode',
  'compare.col.designation': 'Omschrijving',
  'compare.col.invoicePrice': 'Factuurprijs',
  'compare.col.erpPrice': 'ERP-prijs',
  'compare.col.delta': 'Delta',
  'compare.label.number': 'Nummer',
  'compare.label.supplier': 'Leverancier',
  'compare.label.client': 'Klant',
  'compare.label.date': 'Datum',
  'compare.label.invoice': 'Factuur',
  'compare.noNumber': 'Zonder nummer',
  'compare.snack.selectInvoiceAndBl': 'Selecteer een factuur en een LB',
  'compare.snack.linkSuccess': 'Relatie succesvol aangemaakt',
  'compare.snack.linkError': 'Fout bij aanmaken van de relatie',
  'compare.snack.comparisonDone': 'Vergelijking uitgevoerd',
  'compare.snack.comparisonError': 'Fout bij vergelijking',
  'compare.snack.globalComparisonDone': 'Globale vergelijking factuur vs totaal LB uitgevoerd',
  'compare.snack.selectTwoInvoices': 'Selecteer twee facturen',
  'compare.snack.priceComparisonDone': 'Prijsvergelijking uitgevoerd',
  'compare.snack.erpComparisonDone': 'Vergelijking met ERP-prijzen uitgevoerd',
  'compare.snack.stockUpdated': 'Voorraad bijgewerkt (geleverde hoeveelheden)',
  'compare.snack.stockCorrected': 'Voorraad bijgewerkt met gecorrigeerde hoeveelheden',
  'compare.snack.stockSkippedDiffs': 'Verschillen gedetecteerd: voorraad niet bijgewerkt',
  'compare.snack.noLinkedBl': 'Geen LB gekoppeld aan deze factuur',
  'compare.snack.reparseSuccess': 'Document opnieuw geparsed',
  'compare.snack.reparseError': 'Fout bij herparsen',
  'compare.snack.excelDownloaded': 'Excel-export gedownload',
  'compare.confirm.unlink': 'Ontkoppeling bevestigen?',

  'stock.title': 'Voorraadbeheer',
  'stock.subtitle': 'Voorraad bijgewerkt vanuit leveringsbonnen',
  'stock.placeholder': 'Zoek een product of code...',
  'stock.search': 'Zoeken',
  'stock.stat.productCount': 'Totaal producten',
  'stock.stat.totalQty': 'Totaal hoeveelheden',
  'stock.sortByCode': 'Sorteren op: Code',
  'stock.empty': 'Geen producten in voorraad',
  'stock.productCount': '({count} product)',
  'stock.productCountPlural': '({count} producten)',
  'stock.totalQuantity': 'Totale hoeveelheid:',
  'stock.col.code': 'Code',
  'stock.col.label': 'Omschrijving',
  'stock.col.quantity': 'Hoeveelheid',
  'stock.col.unit': 'Eenheid',
  'stock.col.lastUpdated': 'Laatst bijgewerkt',
  'stock.negativeTooltip': 'Negatieve voorraad',
  'stock.unspecifiedSupplier': 'Niet gespecificeerd',
  'stock.snack.loadError': 'Fout bij laden van de voorraad',
  'stock.tab.onHand': 'Actuele voorraad',
  'stock.tab.movements': 'Bewegingen',
  'stock.adjust': 'Voorraad aanpassen',
  'stock.movements.empty': 'Geen voorraadbewegingen',
  'stock.movements.loadError': 'Fout bij laden van bewegingen',
  'stock.movements.col.date': 'Datum',
  'stock.movements.col.type': 'Type',
  'stock.movements.col.code': 'Code',
  'stock.movements.col.qty': 'Hoeveelheid',
  'stock.movements.col.reason': 'Reden',
  'stock.movements.col.ref': 'Referentie',
  'stock.movements.col.by': 'Door',
  'stock.adjust.title': 'Handmatige voorraadbeweging',
  'stock.adjust.productKey': 'Productcode',
  'stock.adjust.type': 'Bewegingstype',
  'stock.adjust.quantity': 'Hoeveelheid',
  'stock.adjust.reason': 'Reden',
  'stock.adjust.reference': 'Referentie',
  'stock.adjust.submit': 'Opslaan',
  'stock.adjust.cancel': 'Annuleren',
  'stock.adjust.success': 'Beweging opgeslagen',
  'stock.adjust.error': 'Fout bij opslaan van de beweging',
  'stock.type.In': 'In',
  'stock.type.Out': 'Uit',
  'stock.type.Adjustment': 'Correctie',
  'stock.type.Transfer': 'Transfer',
  'stock.filter.movements': 'Filter bewegingen (code)...',

  'erpProducts.title': 'ERP-producten',
  'erpProducts.subtitle': 'Lokale fiches raadplegen en unitair synchroniseren met de webservice',
  'erpProducts.enrichFromErp': 'Verrijken vanuit ERP',
  'erpProducts.syncing': 'Sync bezig…',
  'erpProducts.changes': 'Wijzigingen',
  'erpProducts.refresh': 'Vernieuwen',
  'erpProducts.scope': 'Bereik : {label}',
  'erpProducts.cancel': 'Annuleren',
  'erpProducts.stat.total': 'Totaal',
  'erpProducts.stat.page': 'Pagina',
  'erpProducts.searchPlaceholder': 'Naam, ref, EAN, ERP-ID, merk…',
  'erpProducts.filter.allBrands': '— Alle merken —',
  'erpProducts.filter.mainCategory': '— Hoofdcategorie —',
  'erpProducts.filter.subCategory': '— Subcategorie —',
  'erpProducts.filter.subSubCategory': '— Sub-subcategorie —',
  'erpProducts.filter.allSources': 'Alle bronnen',
  'erpProducts.filter.sourceExcel': 'Excel',
  'erpProducts.filter.sourceMerged': 'Excel + ERP',
  'erpProducts.filter.sourceErp': 'Alleen ERP',
  'erpProducts.filter': 'Filteren',
  'erpProducts.syncFiltered': 'Gefilterde sync',
  'erpProducts.reset': 'Resetten',
  'erpProducts.loading': 'Laden…',
  'erpProducts.empty': 'Geen product gevonden',
  'erpProducts.importExcel': 'Importeren vanuit Excel',
  'erpProducts.col.product': 'Product',
  'erpProducts.col.refEan': 'Ref / EAN',
  'erpProducts.col.brand': 'Merk',
  'erpProducts.col.price': 'Prijs',
  'erpProducts.col.stock': 'Voorraad',
  'erpProducts.col.source': 'Bron',
  'erpProducts.col.sync': 'Sync',
  'erpProducts.tooltip.syncErp': 'Synchroniseren met ERP',
  'erpProducts.pageInfo': 'Pagina {page} / {totalPages}',
  'erpProducts.close': 'Sluiten',
  'erpProducts.syncErp': 'ERP-sync',
  'erpProducts.syncingShort': 'Sync…',
  'erpProducts.detail.erpId': 'ERP-ID',
  'erpProducts.detail.reference': 'Referentie',
  'erpProducts.detail.ean': 'EAN',
  'erpProducts.detail.brand': 'Merk',
  'erpProducts.detail.salePrice': 'Verkoopprijs',
  'erpProducts.detail.purchasePrice': 'Aankoopprijs',
  'erpProducts.detail.stock': 'Voorraad',
  'erpProducts.detail.vat': 'BTW %',
  'erpProducts.detail.category': 'Categorie',
  'erpProducts.detail.name2': 'Naam 2',
  'erpProducts.detail.source': 'Bron',
  'erpProducts.detail.excelFile': 'Excel-bestand',
  'erpProducts.detail.lastSync': 'Laatste sync',
  'erpProducts.detail.updated': 'Bijgewerkt',
  'erpProducts.detail.comment': 'Opmerking',
  'erpProducts.progress.fullCatalog': 'Volledige ERP-catalogus sync',
  'erpProducts.progress.filtered': 'Gefilterde producten sync',
  'erpProducts.progress.enrich': 'ERP-verrijking (lokale producten)',
  'erpProducts.snack.loadError': 'Fout bij laden van producten',
  'erpProducts.snack.enrichStarted': 'ERP-verrijking gestart…',
  'erpProducts.snack.syncCancelled': 'Sync geannuleerd',
  'erpProducts.snack.syncFailed': 'Start van ERP-synchronisatie mislukt',
  'erpProducts.snack.productSyncOk': 'Sync OK — {name}',

  'erpChanges.title': 'ERP-wijzigingen',
  'erpChanges.subtitle': 'Opvolging van wijzigingen op EuroBrico-webserviceproducten',
  'erpChanges.enrichFromErp': 'Verrijken vanuit ERP',
  'erpChanges.syncing': 'Sync bezig…',
  'erpChanges.importExcel': 'Excel importeren',
  'erpChanges.importing': 'Import…',
  'erpChanges.excelPlusSync': 'Excel + ERP-sync',
  'erpChanges.refresh': 'Vernieuwen',
  'erpChanges.progressTitle': 'ERP-verrijking bezig',
  'erpChanges.stat.filteredTotal': 'Totaal gefilterd',
  'erpChanges.stat.unreadPage': 'Ongelezen (pagina)',
  'erpChanges.stat.lastSync': 'Laatste sync',
  'erpChanges.filter.unreadOnly': 'Alleen ongelezen',
  'erpChanges.filter.allTypes': 'Alle types',
  'erpChanges.filter.created': 'Aanmaak',
  'erpChanges.filter.updated': 'Wijziging',
  'erpChanges.filter.price': 'Prijs',
  'erpChanges.filter.stock': 'Voorraad',
  'erpChanges.filter.deleted': 'Verwijdering',
  'erpChanges.filter.allValues': 'Alle waarden',
  'erpChanges.filter.bothValues': 'Voor en na ingevuld',
  'erpChanges.filter.cleared': 'Waarde gewist (→ —)',
  'erpChanges.filter.added': 'Waarde toegevoegd (— →)',
  'erpChanges.searchPlaceholder': 'Zoek product, ref, EAN, waarde…',
  'erpChanges.markSelected': 'Selectie markeren',
  'erpChanges.deleteSelected': 'Selectie verwijderen',
  'erpChanges.markAllPage': 'Alles markeren (pagina)',
  'erpChanges.cleanupFalsePositives': 'Valse prijspositieven opschonen',
  'erpChanges.cleaning': 'Opschonen…',
  'erpChanges.reset': 'Resetten',
  'erpChanges.loading': 'Wijzigingen laden…',
  'erpChanges.empty': 'Geen ERP-wijziging voor deze filters',
  'erpChanges.startSync': 'Synchronisatie starten',
  'erpChanges.selectAll': 'Alles selecteren',
  'erpChanges.col.date': 'Datum',
  'erpChanges.col.type': 'Type',
  'erpChanges.col.product': 'Product',
  'erpChanges.col.field': 'Veld',
  'erpChanges.col.before': 'Voor',
  'erpChanges.col.after': 'Na',
  'erpChanges.col.status': 'Status',
  'erpChanges.status.read': 'Gelezen',
  'erpChanges.status.unread': 'Ongelezen',
  'erpChanges.pageInfo': 'Pagina {page} / {totalPages}',
  'erpChanges.syncLogsTitle': 'Laatste synchronisaties',
  'erpChanges.col.started': 'Gestart',
  'erpChanges.col.new': 'Nieuw',
  'erpChanges.col.updated': 'Bijgewerkt',
  'erpChanges.col.failed': 'Mislukt',
  'erpChanges.col.changes': 'Wijzigingen',
  'erpChanges.snack.loadError': 'Fout bij laden van ERP-wijzigingen',
  'erpChanges.snack.selectAtLeastOne': 'Selecteer minstens één wijziging',
  'erpChanges.snack.markedRead': '{count} wijziging(en) gemarkeerd als gelezen',
  'erpChanges.snack.deleted': '{count} wijziging(en) verwijderd',
  'erpChanges.snack.enrichStarted': 'ERP-verrijking gestart…',
  'erpChanges.snack.importExcel': 'Excel-import bezig…',
  'erpChanges.snack.importExcelSync': 'Excel-import + ERP-sync…',
  'erpChanges.snack.importFailed': 'Excel-import mislukt',

  'common.save': 'Opslaan',
  'common.saving': 'Opslaan…',
  'common.edit': 'Bewerken',
  'common.delete': 'Verwijderen',
  'common.actions': 'Acties',
  'common.status': 'Status',
  'common.date': 'Datum',
  'common.notes': 'Notities',
  'common.name': 'Naam',
  'common.code': 'Code',
  'common.email': 'E-mail',
  'common.phone': 'Telefoon',
  'common.city': 'Stad',
  'common.address': 'Adres',
  'common.postalCode': 'Postcode',
  'common.country': 'Land',
  'common.ht': 'excl. btw',
  'common.vat': 'btw',
  'common.ttc': 'incl. btw',
  'common.qty': 'Aantal',
  'common.description': 'Beschrijving',
  'common.ref': 'Ref',
  'common.totalHt': 'Totaal excl. btw',
  'common.totalVat': 'Totaal btw',
  'common.totalTtc': 'Totaal incl. btw',
  'common.unitPriceHt': 'Eenheidsprijs excl. btw',
  'common.vatPercent': 'btw %',
  'common.noLines': 'Geen regels.',
  'common.customer': 'Klant',
  'common.supplier': 'Leverancier',
  'common.active': 'Actief',
  'common.inactive': 'Inactief',
  'common.amountHt': 'Bedrag excl. btw',
  'common.amountTtc': 'Bedrag incl. btw',
  'common.error': 'Fout.',
  'common.detail': 'Detail',
  'common.none': '— geen —',
  'common.optionalNone': '-- Geen --',
  'common.notProvided': 'niet ingevuld',
  'common.noNumber': 'zonder nummer',
  'common.select': '-- Selecteren --',
  'common.addLine': '+ Regel toevoegen',
  'common.addLineShort': '+ Regel',
  'common.updated': 'bijgewerkt',
  'common.created': 'aangemaakt',
  'common.label': 'Omschrijving',

  'sales.title': 'Verkoop & Klanten',
  'sales.subtitle': 'Offerte → Bestelling → Factuur → Betaling / Creditnota',
  'sales.searchPlaceholder': 'Zoeken (Klant, Ref, Nr)...',
  'sales.btn.newInvoice': 'Nieuwe factuur',
  'sales.btn.newOrder': 'Nieuwe bestelling',
  'sales.btn.newQuote': 'Nieuwe offerte',
  'sales.btn.newDeliveryNote': 'Nieuwe leveringsbon',
  'sales.btn.newCustomer': 'Nieuwe klant',
  'sales.tab.invoices': 'Klantfacturen',
  'sales.tab.orders': 'Klantbestellingen',
  'sales.tab.quotes': 'Offertes',
  'sales.tab.creditNotes': 'Creditnota\'s',
  'sales.tab.deliveryNotes': 'Leveringsbonnen',
  'sales.tab.customers': 'Klantenlijst',
  'sales.col.invoiceNumber': 'Factuurnr',
  'sales.col.orderNumber': 'Bestelnr',
  'sales.col.quoteNumber': 'Offertenr',
  'sales.col.creditNoteNumber': 'Creditnotanr',
  'sales.col.deliveryNumber': 'LBn',
  'sales.col.dueDate': 'Vervaldatum',
  'sales.col.paid': 'Betaald',
  'sales.col.expiration': 'Vervaldatum',
  'sales.col.linkedInvoice': 'Gekoppelde factuur',
  'sales.col.order': 'Bestelling',
  'sales.col.vatNumber': 'btw',
  'sales.col.balance': 'Saldo',
  'sales.col.delivered': 'Geleverd',
  'sales.col.ordered': 'Besteld',
  'sales.col.orderedQty': 'Besteld aantal',
  'sales.col.deliveredQty': 'Geleverd aantal',
  'sales.col.codeRef': 'Code / Ref',
  'sales.col.linkedOrder': 'Gekoppelde bestelling',
  'sales.btn.pay': 'Betalen',
  'sales.btn.createCreditNote': 'Creditnota maken',
  'sales.btn.createDeliveryNote': 'LB maken',
  'sales.btn.invoice': 'Factureren',
  'sales.btn.order': 'Bestellen',
  'sales.btn.validate': 'Valideren',
  'sales.btn.apply': 'Toepassen',
  'sales.btn.createInvoice': 'Factuur maken',
  'sales.btn.createOrder': 'Bestelling maken',
  'sales.btn.createQuote': 'Offerte maken',
  'sales.btn.createDeliveryNoteLink': 'LB maken',
  'sales.btn.createCustomer': 'Klant maken',
  'sales.btn.newCustomerLink': '+ Nieuwe klant',
  'sales.btn.validatePayment': 'Betaling bevestigen',
  'sales.btn.createCustomerSubmit': 'Klant aanmaken',
  'sales.empty.invoices': 'Geen factuur.',
  'sales.empty.orders': 'Geen bestelling.',
  'sales.empty.quotes': 'Geen offerte.',
  'sales.empty.creditNotes': 'Geen creditnota gevonden.',
  'sales.empty.deliveryNotes': 'Geen leveringsbon.',
  'sales.empty.customers': 'Geen klant.',
  'sales.customerHash': 'Klant #{id}',
  'sales.label.customer': 'Klant:',
  'sales.label.date': 'Datum:',
  'sales.label.status': 'Status:',
  'sales.label.paid': 'Betaald:',
  'sales.label.ht': 'excl. btw:',
  'sales.label.vat': 'btw:',
  'sales.label.ttc': 'incl. btw:',
  'sales.label.delivered': 'Geleverd: {qty}',
  'sales.label.ordered': 'Besteld: {qty}',
  'sales.selectCustomer': '-- Selecteer een klant --',
  'sales.notesPlaceholder': 'Interne notities / voorwaarden',
  'sales.modal.editCustomer': 'Klant bewerken',
  'sales.modal.newCustomer': 'Nieuwe klant',
  'sales.modal.payment': 'Betaling registreren',
  'sales.modal.newDeliveryNote': 'Nieuwe leveringsbon',
  'sales.modal.newQuote': 'Nieuwe offerte',
  'sales.modal.newOrder': 'Nieuwe bestelling',
  'sales.modal.newInvoice': 'Nieuwe factuur',
  'sales.customer.codeAuto': 'Code (auto indien leeg)',
  'sales.customer.codePlaceholder': 'CUST-...',
  'sales.customer.nameRequired': 'Naam *',
  'sales.customer.namePlaceholder': 'Bedrijfsnaam / naam',
  'sales.customer.vatNumber': 'btw-nr',
  'sales.customer.required': 'Klant *',
  'sales.payment.invoiceNumber': 'Factuurnr',
  'sales.payment.amount': 'Te betalen bedrag :',
  'sales.payment.method': 'Betaalwijze :',
  'sales.payment.cash': 'Contant',
  'sales.payment.card': 'Bankkaart',
  'sales.payment.transfer': 'Overschrijving',
  'sales.lines': 'Regels',
  'sales.needCustomerFirst': 'Maak eerst een klant aan in het tabblad Klantenlijst.',
  'sales.pdfDownloaded': 'PDF gedownload: {fileName}',
  'sales.pdfError': 'PDF-download mislukt.',
  'sales.selectCustomerError': 'Selecteer een klant.',
  'sales.addLineError': 'Voeg minstens één regel toe.',
  'sales.quoteCreated': 'Offerte {number} aangemaakt.',
  'sales.orderCreated': 'Bestelling {number} aangemaakt.',
  'sales.invoiceCreated': 'Factuur {number} aangemaakt.',
  'sales.action.createQuote': 'aanmaken van de offerte',
  'sales.action.createOrder': 'aanmaken van de bestelling',
  'sales.action.createInvoice': 'aanmaken van de factuur',
  'sales.confirm.deleteCustomer': 'Klant « {name} » verwijderen ?',
  'sales.customerDeleted': 'Klant {name} verwijderd.',
  'sales.customerDeleteError': 'Kan deze klant niet verwijderen.',
  'sales.customerNameRequired': 'De klantnaam is verplicht.',
  'sales.customerSaved': 'Klant {name} ({code}) {verb}.',
  'sales.customerSaveError': 'Fout bij {action} van de klant.',
  'sales.customerUpdateAction': 'het bijwerken',
  'sales.customerCreateAction': 'het aanmaken',
  'sales.orderFromQuote': 'Bestelling {order} aangemaakt vanuit offerte {quote}.',
  'sales.quoteToOrderError': 'Conversie offerte → bestelling mislukt.',
  'sales.invoiceFromOrder': 'Factuur {invoice} aangemaakt vanuit bestelling {order}.',
  'sales.orderToInvoiceError': 'Conversie bestelling → factuur mislukt.',
  'sales.paymentSaved': 'Betaling van {amount} € geregistreerd.',
  'sales.paymentError': 'Betaling mislukt.',
  'sales.creditNoteFromInvoice': 'Creditnota {creditNote} aangemaakt vanuit factuur {invoice}.',
  'sales.creditNoteCreateError': 'Fout bij het aanmaken van de creditnota.',
  'sales.creditNoteValidated': 'Creditnota {number} gevalideerd.',
  'sales.creditNoteValidateError': 'Fout bij validatie van de creditnota.',
  'sales.creditNoteApplied': 'Creditnota {number} toegepast.',
  'sales.creditNoteApplyError': 'Fout bij toepassen van de creditnota.',
  'sales.detailLoadError': 'Kan detail niet laden.',
  'sales.detailTitle.quote': 'Offerte {number}',
  'sales.detailTitle.order': 'Bestelling {number}',
  'sales.detailTitle.invoice': 'Factuur {number}',
  'sales.detailTitle.creditNote': 'Creditnota {number}',
  'sales.detailTitle.deliveryNote': 'LB {number}',
  'sales.errorDuring': 'Fout tijdens {action}.',
  'sales.deliveryNoteCreated': 'LB {number} aangemaakt.',
  'sales.deliveryNoteCreateError': 'Fout bij aanmaken van de LB.',
  'sales.deliveryNoteFromOrder': 'LB {delivery} aangemaakt vanuit bestelling {order}.',
  'sales.invoiceFromDeliveryNote': 'Factuur {invoice} aangemaakt vanuit LB {delivery}.',
  'sales.genericError': 'Fout.',
  'sales.pdfGenericError': 'PDF-fout.',
  'sales.confirm.deleteDeliveryNote': 'LB {number} verwijderen ?',
  'sales.deliveryNoteDeleted': 'LB {number} verwijderd.',
  'sales.deleteError': 'Verwijderfout.',

  'purchases.title': 'Aankopen & Leveranciers',
  'purchases.subtitle': 'Opvolging van leveranciersbestellingen en generatie van leveranciersfacturen vanuit geparsede documenten.',
  'purchases.searchPlaceholder': 'Zoeken (leverancier, nummer, notitie)...',
  'purchases.btn.parsedDocuments': 'Geparsede documenten',
  'purchases.btn.uploadDocument': 'Document uploaden',
  'purchases.btn.newInvoice': 'Nieuwe factuur',
  'purchases.btn.newOrder': 'Nieuwe bestelling',
  'purchases.btn.newSupplier': 'Nieuwe leverancier',
  'purchases.tab.supplierInvoices': 'Leveranciersfacturen',
  'purchases.tab.purchaseOrders': 'Leveranciersbestellingen',
  'purchases.tab.parsedDocuments': 'Geparsede documenten',
  'purchases.tab.receipts': 'Ontvangsten',
  'purchases.tab.suppliers': 'Leverancierslijst',
  'purchases.col.invoiceNumber': 'Factuurnr',
  'purchases.col.document': 'Document',
  'purchases.col.order': 'Bestelling',
  'purchases.col.dueDate': 'Vervaldatum',
  'purchases.col.orderNumber': 'Bestelnr',
  'purchases.col.expectedDelivery': 'Verwachte levering',
  'purchases.col.received': 'Ontvangen',
  'purchases.col.documentNumber': 'Documentnr',
  'purchases.col.type': 'Type',
  'purchases.col.file': 'Bestand',
  'purchases.col.documentDate': 'Documentdatum',
  'purchases.col.addedAt': 'Toegevoegd op',
  'purchases.col.target': 'Doel',
  'purchases.col.receiptNumber': 'Ontvangstnr',
  'purchases.col.sourceDocument': 'Brondocument',
  'purchases.col.cfa': 'CFA',
  'purchases.col.lines': 'Regels',
  'purchases.col.qtyReceived': 'Ontvangen aantal',
  'purchases.col.vatNumber': 'btw',
  'purchases.btn.linkDocument': 'Document koppelen',
  'purchases.btn.matchOrder': 'Bestelling afstemmen',
  'purchases.btn.receiveDelivery': 'LB ontvangen',
  'purchases.btn.comptabiliser': 'Boeken',
  'purchases.btn.linkDocumentSubmit': 'Document koppelen',
  'purchases.btn.match': 'Afstemmen',
  'purchases.btn.applyDelivery': 'Leveringsbon toepassen',
  'purchases.btn.createSupplier': 'Leverancier aanmaken',
  'purchases.btn.newSupplierLink': '+ Nieuwe leverancier',
  'purchases.btn.saveInvoice': 'Factuur opslaan',
  'purchases.btn.saveOrder': 'Bestelling opslaan',
  'purchases.empty.supplierInvoices': 'Geen leveranciersfactuur gevonden.',
  'purchases.empty.purchaseOrders': 'Geen leveranciersbestelling gevonden.',
  'purchases.empty.parsedDocuments': 'Geen geparsed document (factuur of LB).',
  'purchases.empty.receipts': 'Geen ontvangst. Boek een LB-document.',
  'purchases.empty.suppliers': 'Geen leverancier. Maak er een aan om aankopen te starten.',
  'purchases.hint.parsedDocuments': 'OCR-documenten (Documents). Boeken maakt een leveranciersfactuur of ontvangst (ErpReceipts).',
  'purchases.hint.receipts': 'Bedrijfsontvangsten (ErpReceipts) na boeking van een geparsede LB.',
  'purchases.status.comptabilise': 'Geboekt',
  'purchases.status.pending': 'In afwachting',
  'purchases.type.invoice': 'Factuur',
  'purchases.type.deliveryNote': 'Leveringsbon',
  'purchases.target.invoiceFo': 'FO-factuur',
  'purchases.target.receipt': 'Ontvangst',
  'purchases.docHash': 'Doc #{id}',
  'purchases.supplierHash': '#{id}',
  'purchases.label.supplier': 'Leverancier:',
  'purchases.label.date': 'Datum:',
  'purchases.label.status': 'Status:',
  'purchases.label.document': 'Document:',
  'purchases.label.order': 'Bestelling:',
  'purchases.label.expectedDelivery': 'Verwachte levering:',
  'purchases.parsedHintPrefix': 'OCR-documenten (',
  'purchases.parsedHintMid': '). ',
  'purchases.parsedHintComptabiliser': 'Boeken',
  'purchases.parsedHintCreates': ' maakt een ',
  'purchases.parsedHintOr': ' of een ',
  'purchases.modal.comptabiliserFromDoc': 'Factuur boeken vanuit een document',
  'purchases.modal.newSupplierInvoice': 'Nieuwe leveranciersfactuur',
  'purchases.modal.linkDocument': 'Document koppelen aan leveranciersfactuur',
  'purchases.modal.newPurchaseOrder': 'Nieuwe leveranciersbestelling',
  'purchases.modal.editSupplier': 'Leverancier bewerken',
  'purchases.modal.newSupplier': 'Nieuwe leverancier',
  'purchases.modal.matchOrder': 'Leveranciersfactuur afstemmen op bestelling',
  'purchases.modal.receiveDelivery': 'Leveringsbon ontvangen',
  'purchases.modal.comptabiliserDoc': 'Document boeken',
  'purchases.selectSupplier': '-- Selecteer een leverancier --',
  'purchases.selectDocument': '-- Selecteer een document --',
  'purchases.selectPurchaseOrder': '-- Selecteer een leveranciersbestelling --',
  'purchases.selectDeliveryNote': '-- Selecteer een leveringsbon --',
  'purchases.defaultVat': 'Standaard btw (%)',
  'purchases.detectedSupplier': 'Gedetecteerde leverancier op document:',
  'purchases.comptabilising': 'Boeken…',
  'purchases.number': 'Nummer',
  'purchases.numberPlaceholder': 'Leeg laten voor autonummering',
  'purchases.notesPlaceholder': 'Optionele opmerking',
  'purchases.invoiceLines': 'Factuurregels',
  'purchases.orderLines': 'Bestelregels',
  'purchases.invoiceLabel': 'Factuur:',
  'purchases.orderLabel': 'Bestelling:',
  'purchases.match.balanced': 'Afstemming in evenwicht',
  'purchases.match.gaps': 'Afwijkingen gedetecteerd',
  'purchases.match.invoiceHt': 'Totaal excl. btw factuur:',
  'purchases.match.orderHt': 'Totaal excl. btw bestelling:',
  'purchases.match.deltaHt': 'Delta excl. btw:',
  'purchases.match.matchedLines': 'Gematchte regels:',
  'purchases.match.qtyGaps': 'Aantalafwijkingen:',
  'purchases.match.priceGaps': 'Prijsafwijkingen:',
  'purchases.match.moreWarnings': '{count} andere afwijking(en) niet getoond.',
  'purchases.supplier.codeAuto': 'Code (auto indien leeg)',
  'purchases.supplier.codePlaceholder': 'SUP-...',
  'purchases.supplier.nameRequired': 'Naam *',
  'purchases.supplier.namePlaceholder': 'Bedrijfsnaam',
  'purchases.supplier.vatNumber': 'btw-nr',
  'purchases.supplier.required': 'Leverancier *',
  'purchases.purchaseOrderOptional': 'Leveranciersbestelling (optioneel)',
  'purchases.parsedDocument': 'Geparsed document',
  'purchases.deliveryDocument': 'Geparsede leveringsbon',
  'purchases.purchaseOrder': 'Leveranciersbestelling',
  'purchases.hint.invoiceCreates': 'Maakt een geboekte leveranciersfactuur (status Validated).',
  'purchases.hint.deliveryCreates': 'Maakt een ontvangst (ErpReceipts) en werkt de voorraad bij.',
  'purchases.autoCreated': 'Leveranciersfactuur #{id} automatisch aangemaakt vanuit parsing.',
  'purchases.needSupplierFirst': 'Maak eerst een leverancier aan.',
  'purchases.pdfDownloaded': 'PDF gedownload: {fileName}',
  'purchases.pdfError': 'PDF-download mislukt.',
  'purchases.confirm.deleteSupplier': 'Leverancier « {name} » verwijderen ?',
  'purchases.supplierDeleted': 'Leverancier {name} verwijderd.',
  'purchases.selectSupplierError': 'Selecteer een leverancier.',
  'purchases.selectSupplierAndDoc': 'Selecteer een leverancier en een document.',
  'purchases.invoiceComptabilised': 'Factuur geboekt → {number}.{warnings}',
  'purchases.blComptabilised': 'LB geboekt → ontvangst {number} (ErpReceipts).',
  'purchases.stockAlreadyFed': 'Voorraad al bijgewerkt (geen dubbele boeking).',
  'purchases.invoiceFromDoc': 'Factuur {number} geboekt vanuit document.',
  'purchases.supplierInvoiceCreated': 'Leveranciersfactuur {number} aangemaakt.',
  'purchases.supplierInvoiceCreateError': 'Fout bij aanmaken van de leveranciersfactuur.',
  'purchases.purchaseOrderCreated': 'Leveranciersbestelling {number} aangemaakt.',
  'purchases.purchaseOrderCreateError': 'Fout bij aanmaken van de leveranciersbestelling.',
  'purchases.supplierSaved': 'Leverancier {name} ({code}) {verb}.',
  'purchases.supplierSaveError': 'Fout bij {action} van de leverancier.',
  'purchases.supplierUpdateAction': 'het bijwerken',
  'purchases.supplierCreateAction': 'het aanmaken',
  'purchases.selectDocumentError': 'Selecteer een document.',
  'purchases.documentLinked': 'Document gekoppeld aan leveranciersfactuur.',
  'purchases.selectDeliveryError': 'Selecteer een leveringsbon.',
  'purchases.receiveDeliveryError': 'Fout bij ontvangst van de leveringsbon.',
  'purchases.selectPurchaseOrderError': 'Selecteer een leveranciersbestelling.',
  'purchases.matchPreviewError': 'Fout bij voorvertoning van de afstemming.',
  'purchases.detailLoadError': 'Kan detail niet laden.',
  'purchases.detailTitle.order': 'Bestelling {number}',
  'purchases.detailTitle.invoice': 'Factuur {number}',
  'purchases.detailTitle.receipt': 'Ontvangst {number}',
  'purchases.addLineError': 'Voeg minstens één regel toe.',
  'purchases.supplierNameRequired': 'De naam van de leverancier is verplicht.',
  'purchases.documentMissing': 'Document ontbreekt.',
  'purchases.comptabiliseError': 'Fout bij het boeken.',
  'purchases.supplierDeleteError': 'Kan deze leverancier niet verwijderen.',
  'purchases.linkDocumentError': 'Fout bij het koppelen van het document.',
  'purchases.matchError': 'Fout bij het afstemmen van de leveranciersfactuur.',
  'purchases.supplierNameHash': 'Leverancier #{id}',
  'purchases.stockUpdated': 'Voorraad +{qty} ({count} bewegingen).',
  'purchases.receiveStockEntry': 'Voorraad ingang: {qty} eenheid(en) voor {count} product(en).',
  'purchases.receiveStockAlreadyFed': 'Voorraad al aangevuld voor deze LB (geen dubbele ingang).',
  'purchases.matchOk': 'Afstemming uitgevoerd tussen factuur {invoice} en bestelling {order}.',
  'purchases.matchWithGaps': 'Afstemming met afwijkingen tussen factuur {invoice} en bestelling {order}.',
  'purchases.receiveApplied': 'LB-ontvangst toegepast op bestelling {order}.',

  'cash.title': 'Kassa & Financiën',
  'cash.subtitle': 'Opening, verrichtingen en afsluiting van de kassasessie.',
  'cash.btn.newOperation': 'Nieuwe verrichting',
  'cash.btn.close': 'Kassa afsluiten',
  'cash.btn.open': 'Kassa openen',
  'cash.tab.active': 'Actieve sessie',
  'cash.tab.history': 'Geschiedenis',
  'cash.noSession.title': 'Er is momenteel geen kassasessie geopend.',
  'cash.noSession.hint': 'Open een sessie om kassaverkopen en stortingen/opnames te registreren.',
  'cash.metric.sessionNumber': 'Sessienr',
  'cash.metric.openingBalance': 'Beginfonds',
  'cash.metric.theoretical': 'Theoretisch saldo',
  'cash.metric.inOut': 'Inkomsten / Uitgaven',
  'cash.metric.openedBy': 'Geopend door',
  'cash.metric.status': 'Status',
  'cash.operationsTitle': 'Sessieverrichtingen',
  'cash.col.date': 'Datum',
  'cash.col.operationType': 'Type verrichting',
  'cash.col.description': 'Beschrijving',
  'cash.col.reference': 'Referentie',
  'cash.col.amount': 'Bedrag',
  'cash.col.author': 'Auteur',
  'cash.col.type': 'Type',
  'cash.col.number': 'Nr',
  'cash.col.opening': 'Opening',
  'cash.col.status': 'Status',
  'cash.col.float': 'Fonds',
  'cash.col.closing': 'Afsluiting',
  'cash.col.variance': 'Verschil',
  'cash.empty.operations': 'Geen verrichting in deze sessie.',
  'cash.empty.sessions': 'Geen sessie geregistreerd.',
  'cash.empty.historyOps': 'Geen verrichting.',
  'cash.historyTitle': 'Recente sessies',
  'cash.detailTitle': 'Detail — {number}',
  'cash.openedAt': 'Geopend: {date} door {by}',
  'cash.closedAt': 'Afgesloten: {date} door {by}',
  'cash.expected': 'Verwacht: {amount}',
  'cash.modal.open': 'Kassa openen',
  'cash.modal.close': 'Kassa afsluiten',
  'cash.modal.newOp': 'Nieuwe kassaverrichting',
  'cash.openingBalanceLabel': 'Openingsfonds (€) :',
  'cash.confirmOpen': 'Opening bevestigen',
  'cash.theoreticalLabel': 'Theoretisch saldo:',
  'cash.realCountLabel': 'Werkelijke telling (€) :',
  'cash.varianceLabel': 'Verschil:',
  'cash.opTypeLabel': 'Type verrichting :',
  'cash.op.deposit': 'Contante storting',
  'cash.op.withdrawal': 'Kassaopname',
  'cash.op.salePayment': 'Directe contante verkoop',
  'cash.amountLabel': 'Bedrag (€) :',
  'cash.descriptionLabel': 'Beschrijving :',
  'cash.descriptionPlaceholder': 'Reden of referentie',
  'cash.referenceLabel': 'Documentreferentie :',
  'cash.referencePlaceholder': 'FAC-... / ticket',
  'cash.opLabel.deposit': 'Storting',
  'cash.opLabel.withdrawal': 'Opname',
  'cash.opLabel.salePayment': 'Contante verkoop',
  'cash.loadSessionError': 'Kan sessie niet laden.',
  'cash.opened': 'Kassasessie geopend.',
  'cash.openError': 'Kan kassa niet openen.',
  'cash.closed': 'Kassa afgesloten. Verschil: {diff} €.',
  'cash.closeError': 'Kan kassa niet afsluiten.',
  'cash.invalidAmount': 'Ongeldig bedrag.',
  'cash.opSaved': 'Verrichting geregistreerd.',
  'cash.opSaveError': 'Kan verrichting niet registreren.',

  'numbering.title': 'Documentnummering',
  'numbering.subtitle': 'Voorvoegsels, formaten en chronologische tellers (offertes, facturen, creditnota\'s, aankopen…).',
  'numbering.initDefaults': 'Standaarden initialiseren',
  'numbering.placeholdersHint': 'Beschikbare placeholders in het formaat :',
  'numbering.example': 'Voorbeeld :',
  'numbering.empty.title': 'Geen reeks geconfigureerd',
  'numbering.empty.hint': 'Klik op « Standaarden initialiseren » om de bedrijfstellers te maken.',
  'numbering.prefix': 'Voorvoegsel',
  'numbering.year': 'Jaar',
  'numbering.nextNumber': 'Volgend nr',
  'numbering.format': 'Formaat',
  'numbering.type.Quote': 'Offerte',
  'numbering.type.Order': 'Klantbestelling',
  'numbering.type.Invoice': 'Klantfactuur',
  'numbering.type.CreditNote': 'Creditnota',
  'numbering.type.PurchaseOrder': 'Leveranciersbestelling',
  'numbering.type.SupplierInvoice': 'Leveranciersfactuur',
  'numbering.type.DeliveryNote': 'Leveringsbon',
  'numbering.loadError': 'Kan reeksen niet laden.',
  'numbering.defaultsReady': 'Standaardreeksen aangemaakt / gecontroleerd.',
  'numbering.initError': 'Kan reeksen niet initialiseren.',
  'numbering.prefixRequired': 'Voorvoegsel is verplicht.',
  'numbering.nextNumberInvalid': 'Volgend nummer moet ≥ 1 zijn.',
  'numbering.formatRequired': 'Formaat is verplicht.',
  'numbering.saved': 'Reeks {type} opgeslagen.',
  'numbering.saveError': 'Opslaan mislukt.',

  'admin.title': 'Administratie',
  'admin.tab.tenants': 'Tenants',
  'admin.tab.companies': 'Bedrijven',
  'admin.tab.roles': 'Rollen',
  'admin.tab.users': 'Gebruikers',
  'admin.btn.newTenant': 'Nieuwe tenant',
  'admin.btn.newCompany': 'Nieuw bedrijf',
  'admin.btn.newRole': 'Nieuwe rol',
  'admin.btn.newUser': 'Nieuwe gebruiker',
  'admin.col.name': 'Naam',
  'admin.col.active': 'Actief',
  'admin.col.companies': 'Bedrijven',
  'admin.col.createdAt': 'Aangemaakt op',
  'admin.col.createdAtF': 'Aangemaakt op',
  'admin.col.language': 'Taal',
  'admin.col.currency': 'Valuta',
  'admin.col.permissions': 'Rechten',
  'admin.col.user': 'Gebruiker',
  'admin.col.email': 'E-mail',
  'admin.col.role': 'Rol',
  'admin.col.activeCompany': 'Actief bedrijf',
  'admin.col.company': 'Bedrijf',
  'admin.col.id': 'ID',
  'admin.empty.tenants': 'Geen tenant.',
  'admin.empty.companies': 'Geen bedrijf.',
  'admin.empty.companiesHint': 'Geen bedrijf.',
  'admin.empty.createCompany': 'Bedrijf aanmaken',
  'admin.empty.roles': 'Geen rol.',
  'admin.empty.users': 'Geen gebruiker.',
  'admin.empty.access': 'Geen toegang.',
  'admin.active': 'Actief',
  'admin.inactive': 'Inactief',
  'admin.activeF': 'Actief',
  'admin.inactiveF': 'Inactief',
  'admin.role.admin': 'Admin',
  'admin.role.user': 'User',
  'admin.companiesAccessible': 'Toegankelijke bedrijven :',
  'admin.modal.newRole': 'Nieuwe rol',
  'admin.modal.editRole': 'Rol bewerken',
  'admin.modal.editTenant': 'Tenant bewerken',
  'admin.modal.newTenant': 'Nieuwe tenant',
  'admin.modal.editCompany': 'Bedrijf bewerken',
  'admin.modal.newCompany': 'Nieuw bedrijf',
  'admin.modal.editUser': 'Gebruiker bewerken',
  'admin.modal.newUser': 'Nieuwe gebruiker',
  'admin.modal.resetPassword': 'Wachtwoord resetten',
  'admin.modal.assign': '{username} toewijzen aan een bedrijf',
  'admin.role.nameRequired': 'Rolnaam *',
  'admin.role.allPermissions': 'Alle rechten',
  'admin.role.adminLocked': 'De rol Admin heeft automatisch alle rechten (niet wijzigbaar).',
  'admin.role.namePlaceholder': 'Bv: Boekhouder, Verkoper…',
  'admin.permissions': 'Rechten',
  'admin.tenant.namePlaceholder': 'Tenantnaam',
  'admin.company.namePlaceholder': 'Bedrijfsnaam',
  'admin.company.tenantRequired': 'Tenant *',
  'admin.user.usernameRequired': 'Gebruikersnaam *',
  'admin.user.usernamePlaceholder': 'Login / e-mail',
  'admin.user.emailPlaceholder': 'email@domein.com',
  'admin.user.firstName': 'Voornaam',
  'admin.user.lastName': 'Naam',
  'admin.user.lastNamePlaceholder': 'Familienaam',
  'admin.user.passwordRequired': 'Wachtwoord *',
  'admin.user.passwordOptional': 'Nieuw wachtwoord (leeg = ongewijzigd)',
  'admin.user.defaultCompany': 'Standaardbedrijf',
  'admin.user.businessRole': 'Bedrijfsrol',
  'admin.user.standardRole': '— Standaardgebruiker —',
  'admin.user.isAdmin': 'Beheerder',
  'admin.user.label': 'Gebruiker :',
  'admin.user.newPassword': 'Nieuw wachtwoord *',
  'admin.user.newPasswordPlaceholder': 'Nieuw wachtwoord',
  'admin.resetting': 'Resetten…',
  'admin.reset': 'Resetten',
  'admin.assigning': 'Toewijzen…',
  'admin.assign': 'Toewijzen',
  'admin.nameRequired': 'Naam *',
  'admin.title.edit': 'Bewerken',
  'admin.title.delete': 'Verwijderen',
  'admin.title.editPermissions': 'Bewerken / rechten',
  'admin.title.resetPassword': 'Wachtwoord resetten',
  'admin.title.assignCompany': 'Bedrijf toewijzen',
  'admin.title.remove': 'Verwijderen',
  'admin.error.nameRequired': 'Naam verplicht.',
  'admin.error.tenantRequired': 'Tenant verplicht.',
  'admin.error.usernameRequired': 'Gebruikersnaam verplicht.',
  'admin.error.passwordRequired': 'Wachtwoord verplicht.',
  'admin.error.roleNameRequired': 'Rolnaam verplicht.',
  'admin.tenantSaved': 'Tenant "{name}" opgeslagen.',
  'admin.companySaved': 'Bedrijf "{name}" opgeslagen.',
  'admin.userAssigned': 'Gebruiker toegewezen aan bedrijf.',
  'admin.confirm.removeAccess': 'Deze gebruiker van het bedrijf verwijderen ?',
  'admin.accessRemoved': 'Toegang verwijderd.',
  'admin.userUpdated': 'Gebruiker bijgewerkt.',
  'admin.userCreated': 'Gebruiker aangemaakt.',
  'admin.confirm.deleteUser': 'Gebruiker "{username}" verwijderen ?',
  'admin.userDeleted': 'Gebruiker "{username}" verwijderd.',
  'admin.passwordReset': 'Wachtwoord gereset voor "{username}".',
  'admin.roleUpdated': 'Rol bijgewerkt.',
  'admin.roleCreated': 'Rol aangemaakt.',
  'admin.confirm.deleteRole': 'Rol "{name}" verwijderen ?',
  'admin.roleDeleted': 'Rol "{name}" verwijderd.',
  'admin.perm.cat.sales': 'Verkoop',
  'admin.perm.cat.purchases': 'Aankopen',
  'admin.perm.cat.stock': 'Voorraad',
  'admin.perm.cat.erp': 'ERP-producten',
  'admin.perm.cat.documents': 'Documenten',
  'admin.perm.cat.cash': 'Kassa',
  'admin.perm.cat.settings': 'Instellingen',
  'admin.perm.cat.admin': 'Administratie',
  'admin.perm.sec.customers': 'Klanten',
  'admin.perm.sec.quotes': 'Offertes',
  'admin.perm.sec.orders': 'Bestellingen',
  'admin.perm.sec.deliveryNotes': 'Leveringsbonnen',
  'admin.perm.sec.invoices': 'Facturen',
  'admin.perm.sec.suppliers': 'Leveranciers',
  'admin.perm.sec.purchaseOrders': 'Aankoopbestellingen',
  'admin.perm.sec.receipts': 'Ontvangsten',
  'admin.perm.sec.supplierInvoices': 'Leveranciersfacturen',
  'admin.perm.sec.stock': 'Voorraad',
  'admin.perm.sec.products': 'Productcatalogus',
  'admin.perm.sec.erpChanges': 'ERP-wijzigingen',
  'admin.perm.sec.documents': 'Documenten',
  'admin.perm.sec.cash': 'Kassa',
  'admin.perm.sec.numbering': 'Nummering',
  'admin.perm.sec.users': 'Gebruikers',
  'admin.perm.sec.roles': 'Rollen',
  'admin.perm.action.Read': 'Lezen',
  'admin.perm.action.Create': 'Aanmaken',
  'admin.perm.action.Update': 'Wijzigen',
  'admin.perm.action.Delete': 'Verwijderen',
  'admin.perm.action.Manage': 'Beheren',
  'admin.perm.action.Upload': 'Importeren',
  'admin.perm.action.Link': 'Koppelen',

  'assistant.title': 'Winkelassistent',
  'assistant.subtitle': 'Productadvies, offerte en bestelling',
  'assistant.project': 'Project',
  'assistant.budget': 'Budget',
  'assistant.cart': 'Winkelwagen',
  'assistant.cartEmpty': 'Geen producten in de winkelwagen.',
  'assistant.close': 'Sluiten',
  'assistant.remove': 'Verwijderen',
  'assistant.welcome': 'Hallo! Ik ben de winkelassistent. Vraag een product, merk of project (verf, elektriciteit…).',
  'assistant.redirecting': 'Doorverwijzen naar de winkelassistent…',
  'assistant.placeholder': 'Bv. witte muurverf 10L, LED-lamp, boormachine…',
  'assistant.send': 'Verzenden',
  'assistant.quote': 'Offerte vragen',
  'assistant.order': 'Bestellen',
  'assistant.downloadQuote': 'Offerte downloaden',
  'assistant.downloadInvoice': 'Factuur downloaden',
  'assistant.payCard': 'Betalen met kaart',
  'assistant.product': 'Product',
  'assistant.price': 'Prijs',
  'assistant.qty': 'Aantal',
  'assistant.error': 'Sorry, er is een fout opgetreden. Probeer opnieuw.',
  'assistant.newProject': 'Nieuw project',
  'assistant.photo': 'Foto toevoegen',
  'assistant.listening': 'Luisteren…',
  'assistant.lang': 'Taal',
  'assistant.nextStep': 'Volgende stap',
  'assistant.next': 'Volgende',
  'assistant.reviewCart': 'Winkelwagen nalopen',
  'assistant.langSwitched': 'Taal: Nederlands.'
};

const EN: Dict = {
  'common.ok': 'OK',
  'common.close': 'Close',
  'common.cancel': 'Cancel',
  'common.reset': 'Reset',
  'common.refresh': 'Refresh',
  'common.loading': 'Loading…',
  'common.prev': 'Previous',
  'common.next': 'Next',
  'common.search': 'Search',
  'common.lang': 'Language',
  'common.show': 'Show',
  'common.hide': 'Hide',

  'nav.brandSub': 'Industrial Logistics',
  'nav.newAnalysis': 'New analysis',
  'nav.logout': 'Sign out',
  'nav.upload': 'Upload',
  'nav.search': 'Search',
  'nav.compare': 'Matching',
  'nav.stock': 'Stock',
  'nav.erpProducts': 'Products',
  'nav.erpChanges': 'Changes',
  'nav.assistant': 'Assistant',
  'nav.assistantTab': 'Store',
  'nav.sales': 'Sales & Customers',
  'nav.purchases': 'Purchases',
  'nav.cash': 'Cash register',
  'nav.numbering': 'Numbering',
  'nav.admin': 'Administration',
  'nav.title.upload': 'Document Management',
  'nav.title.search': 'Search',
  'nav.title.compare': 'Matching',
  'nav.title.stock': 'Document Management',
  'nav.title.erpProducts': 'ERP Products',
  'nav.title.erpChanges': 'ERP Changes',
  'nav.title.assistant': 'Store assistant',
  'nav.title.sales': 'Sales & Customers',
  'nav.title.purchases': 'Purchases',
  'nav.title.cash': 'Cash register',
  'nav.title.numbering': 'Numbering',
  'nav.title.admin': 'Administration',
  'nav.title.default': 'Document Management',

  'accessDenied.title': 'Access denied',
  'accessDenied.message': 'Your account does not have the permissions required for this page.',
  'accessDenied.asUser': 'Signed in as',
  'accessDenied.goHome': 'Go to home',
  'accessDenied.logout': 'Sign out',

  'login.brandSub': 'Sign in',
  'login.title': 'Access the application',
  'login.hint': 'Use your EuroBrico Backup account',
  'login.email': 'Email',
  'login.password': 'Password',
  'login.submit': 'Sign in',
  'login.submitting': 'Signing in…',
  'login.required': 'Email and password required',
  'login.invalid': 'Invalid credentials',

  'upload.title': 'Document Upload',
  'upload.newDocument': 'New Document',
  'upload.dropPlaceholder': 'Drop your file here',
  'upload.dropHint': 'Drag and drop your file here or click to browse',
  'upload.documentType': 'Document Type',
  'upload.type.invoice': 'Invoice',
  'upload.type.deliveryNote': 'Delivery Note',
  'upload.type.other': 'Other',
  'upload.supplier': 'Supplier',
  'upload.selectPlaceholder': '-- Select --',
  'upload.noSuppliersHint': 'No supplier registered. The supplier will be added when the file is inspected.',
  'upload.number': 'Number',
  'upload.numberPlaceholder': 'Document number',
  'upload.client': 'Customer',
  'upload.clientPlaceholder': 'Customer name',
  'upload.date': 'Date',
  'upload.aiEnabled': 'AI extraction enabled',
  'upload.aiAccuracy': 'Accuracy ~99%',
  'upload.submit': 'Upload',
  'upload.submitting': 'Uploading...',
  'upload.reset': 'Reset',
  'upload.recentHistory': 'Recent History',
  'upload.viewAll': 'View all',
  'upload.col.document': 'Document',
  'upload.col.supplier': 'Supplier',
  'upload.col.status': 'Status',
  'upload.col.date': 'Date',
  'upload.col.id': 'ID',
  'upload.col.type': 'Type',
  'upload.col.client': 'Customer',
  'upload.unidentified': 'Unidentified',
  'upload.status.validated': 'Validated',
  'upload.status.error': 'Error',
  'upload.aiMetrics': 'AI Metrics',
  'upload.indexedDocuments': 'Indexed documents',
  'upload.unlinkedTitle': 'Unlinked Documents — {supplier}',
  'upload.download': 'Download',
  'upload.relative.justNow': 'Just now',
  'upload.relative.hoursAgo': '{hours}h ago',
  'upload.relative.daysAgo': '{days}d ago',
  'upload.snack.selectFile': 'Please select a file',
  'upload.snack.success': 'Document uploaded successfully',
  'upload.snack.duplicate': 'This document already exists in the system',
  'upload.snack.uploadError': 'Upload error',
  'upload.snack.linkSuccess': 'DN linked to invoice successfully',
  'upload.snack.linkError': 'Linking error',
  'upload.snack.invoicesFound': '{count} invoices found. Redirecting to matching page...',
  'upload.snack.noInvoice': 'No invoice found with this number. Showing all supplier invoices...',
  'upload.confirm.linkInvoice': 'Invoice found: {numero}\nLink it to this DN?',

  'search.title': 'Document Search',
  'search.subtitle': 'Search by text, number, customer or supplier',
  'search.placeholder': 'Search for a document...',
  'search.submit': 'Search',
  'search.stat.results': 'Results',
  'search.stat.invoices': 'Invoices',
  'search.stat.deliveryNotes': 'Delivery notes',
  'search.activeFilter': 'Active filter: "{query}"',
  'search.loading': 'Searching…',
  'search.empty.welcome': 'Enter a term then click Search',
  'search.empty.hint': 'Number, customer, supplier or document text',
  'search.empty.none': 'No document found',
  'search.resultsTitle': 'Documents',
  'search.col.id': 'ID',
  'search.col.type': 'Type',
  'search.col.number': 'Number',
  'search.col.supplier': 'Supplier',
  'search.col.client': 'Customer',
  'search.col.date': 'Date',
  'search.col.actions': 'Actions',
  'search.type.invoice': 'Invoice',
  'search.type.bl': 'DN',
  'search.type.other': 'Other',
  'search.tooltip.openAssociation': 'Open in Matching',
  'search.tooltip.download': 'Download',
  'search.error.load': 'Unable to load documents. Check that the API is running.',

  'compare.title': 'Document Matching & Comparison',
  'compare.subtitle': 'Automated reconciliation of invoices and delivery notes.',
  'compare.exportAll': 'Export all (Excel)',
  'compare.validateAssociation': 'Validate Match',
  'compare.suggestedInvoices': 'Suggested invoices for DN #{blId}',
  'compare.selection.association': 'Current Selection (Invoice-DN Match)',
  'compare.associate': 'Link',
  'compare.compareErpPrices': 'Compare with ERP prices',
  'compare.priceCompare.title': 'Price Comparison Between Invoices',
  'compare.differentSuppliers': 'Different suppliers',
  'compare.sameSupplierRequired': 'Both invoices must have the same supplier to be compared.',
  'compare.comparePrices': 'Compare Prices',
  'compare.invoices': 'Invoices',
  'compare.status.linked': 'Linked',
  'compare.status.unlinked': 'Unlinked',
  'compare.reparseInvoice': 'Reparse Invoice',
  'compare.associatedDeliveries': 'Linked Delivery Notes',
  'compare.compareInvoiceVsBl': 'Compare invoice vs DN total',
  'compare.allBlCompareStock': 'All DNs: Compare + Stock',
  'compare.allBlStockCorrection': 'All DNs: Stock correction',
  'compare.unlink': 'Unlink',
  'compare.reparseBl': 'Reparse DN',
  'compare.addDelivery': 'Add Delivery Note',
  'compare.noExtraDeliveries': 'No extra delivery note available for matching (same supplier required).',
  'compare.deliveries': 'Delivery notes',
  'compare.empty.deliveries': 'No delivery note in the loaded set.',
  'compare.otherDocuments': 'Other documents',
  'compare.reparse': 'Reparse',
  'compare.detailsComparison': 'Details Comparison',
  'compare.errors': 'ERRORS',
  'compare.excel': 'Excel',
  'compare.col.product': 'Product',
  'compare.col.invoiceQty': 'Invoice Qty',
  'compare.col.deliveryQty': 'DN Qty',
  'compare.col.actualQty': 'Actual Qty',
  'compare.actualQtyLabel': 'Actual quantity',
  'compare.col.qtyDiff': 'Qty Diff',
  'compare.col.currentUnitPrice': 'Current Invoice Unit Price',
  'compare.col.previousUnitPrice': 'Previous Invoice Unit Price',
  'compare.col.priceDiff': 'Price Diff',
  'compare.col.code': 'Code',
  'compare.col.unit': 'Unit',
  'compare.col.totalInvoice': 'Total (Inv)',
  'compare.col.status': 'Status',
  'compare.erpPrice.title': 'ERP Price Comparison',
  'compare.erpPrice.loading': 'Querying ERP web service…',
  'compare.col.productCode': 'Product code',
  'compare.col.designation': 'Description',
  'compare.col.invoicePrice': 'Invoice price',
  'compare.col.erpPrice': 'ERP price',
  'compare.col.delta': 'Delta',
  'compare.label.number': 'Number',
  'compare.label.supplier': 'Supplier',
  'compare.label.client': 'Customer',
  'compare.label.date': 'Date',
  'compare.label.invoice': 'Invoice',
  'compare.noNumber': 'No number',
  'compare.snack.selectInvoiceAndBl': 'Please select an invoice and a DN',
  'compare.snack.linkSuccess': 'Relation created successfully',
  'compare.snack.linkError': 'Error creating relation',
  'compare.snack.comparisonDone': 'Comparison completed',
  'compare.snack.comparisonError': 'Comparison error',
  'compare.snack.globalComparisonDone': 'Global invoice vs DN total comparison completed',
  'compare.snack.selectTwoInvoices': 'Please select two invoices',
  'compare.snack.priceComparisonDone': 'Price comparison completed',
  'compare.snack.erpComparisonDone': 'ERP price comparison completed',
  'compare.snack.stockUpdated': 'Stock updated (delivered quantities)',
  'compare.snack.stockCorrected': 'Stock updated with corrected quantities',
  'compare.snack.stockSkippedDiffs': 'Differences detected: stock not updated',
  'compare.snack.noLinkedBl': 'No DN linked to this invoice',
  'compare.snack.reparseSuccess': 'Document reparsed successfully',
  'compare.snack.reparseError': 'Reparse error',
  'compare.snack.excelDownloaded': 'Excel export downloaded',
  'compare.confirm.unlink': 'Confirm unlink?',

  'stock.title': 'Stock Management',
  'stock.subtitle': 'Stock updated from delivery notes',
  'stock.placeholder': 'Search a product or code...',
  'stock.search': 'Search',
  'stock.stat.productCount': 'Total products',
  'stock.stat.totalQty': 'Total quantities',
  'stock.sortByCode': 'Sort by: Code',
  'stock.empty': 'No products in stock',
  'stock.productCount': '({count} product)',
  'stock.productCountPlural': '({count} products)',
  'stock.totalQuantity': 'Total quantity:',
  'stock.col.code': 'Code',
  'stock.col.label': 'Label',
  'stock.col.quantity': 'Quantity',
  'stock.col.unit': 'Unit',
  'stock.col.lastUpdated': 'Last updated',
  'stock.negativeTooltip': 'Negative stock',
  'stock.unspecifiedSupplier': 'Unspecified',
  'stock.snack.loadError': 'Error loading stock',
  'stock.tab.onHand': 'On-hand stock',
  'stock.tab.movements': 'Movements',
  'stock.adjust': 'Adjust stock',
  'stock.movements.empty': 'No stock movements',
  'stock.movements.loadError': 'Error loading movements',
  'stock.movements.col.date': 'Date',
  'stock.movements.col.type': 'Type',
  'stock.movements.col.code': 'Code',
  'stock.movements.col.qty': 'Quantity',
  'stock.movements.col.reason': 'Reason',
  'stock.movements.col.ref': 'Reference',
  'stock.movements.col.by': 'By',
  'stock.adjust.title': 'Manual stock movement',
  'stock.adjust.productKey': 'Product code',
  'stock.adjust.type': 'Movement type',
  'stock.adjust.quantity': 'Quantity',
  'stock.adjust.reason': 'Reason',
  'stock.adjust.reference': 'Reference',
  'stock.adjust.submit': 'Save',
  'stock.adjust.cancel': 'Cancel',
  'stock.adjust.success': 'Movement saved',
  'stock.adjust.error': 'Error saving movement',
  'stock.type.In': 'In',
  'stock.type.Out': 'Out',
  'stock.type.Adjustment': 'Adjustment',
  'stock.type.Transfer': 'Transfer',
  'stock.filter.movements': 'Filter movements (code)...',

  'erpProducts.title': 'ERP Products',
  'erpProducts.subtitle': 'Browse local records and sync individually with the webservice',
  'erpProducts.enrichFromErp': 'Enrich from ERP',
  'erpProducts.syncing': 'Syncing…',
  'erpProducts.changes': 'Changes',
  'erpProducts.refresh': 'Refresh',
  'erpProducts.scope': 'Scope: {label}',
  'erpProducts.cancel': 'Cancel',
  'erpProducts.stat.total': 'Total',
  'erpProducts.stat.page': 'Page',
  'erpProducts.searchPlaceholder': 'Name, ref, EAN, ERP ID, brand…',
  'erpProducts.filter.allBrands': '— All brands —',
  'erpProducts.filter.mainCategory': '— Main category —',
  'erpProducts.filter.subCategory': '— Subcategory —',
  'erpProducts.filter.subSubCategory': '— Sub-subcategory —',
  'erpProducts.filter.allSources': 'All sources',
  'erpProducts.filter.sourceExcel': 'Excel',
  'erpProducts.filter.sourceMerged': 'Excel + ERP',
  'erpProducts.filter.sourceErp': 'ERP only',
  'erpProducts.filter': 'Filter',
  'erpProducts.syncFiltered': 'Sync filtered',
  'erpProducts.reset': 'Reset',
  'erpProducts.loading': 'Loading…',
  'erpProducts.empty': 'No product found',
  'erpProducts.importExcel': 'Import from Excel',
  'erpProducts.col.product': 'Product',
  'erpProducts.col.refEan': 'Ref / EAN',
  'erpProducts.col.brand': 'Brand',
  'erpProducts.col.price': 'Price',
  'erpProducts.col.stock': 'Stock',
  'erpProducts.col.source': 'Source',
  'erpProducts.col.sync': 'Sync',
  'erpProducts.tooltip.syncErp': 'Sync with ERP',
  'erpProducts.pageInfo': 'Page {page} / {totalPages}',
  'erpProducts.close': 'Close',
  'erpProducts.syncErp': 'ERP Sync',
  'erpProducts.syncingShort': 'Sync…',
  'erpProducts.detail.erpId': 'ERP ID',
  'erpProducts.detail.reference': 'Reference',
  'erpProducts.detail.ean': 'EAN',
  'erpProducts.detail.brand': 'Brand',
  'erpProducts.detail.salePrice': 'Sale price',
  'erpProducts.detail.purchasePrice': 'Purchase price',
  'erpProducts.detail.stock': 'Stock',
  'erpProducts.detail.vat': 'VAT %',
  'erpProducts.detail.category': 'Category',
  'erpProducts.detail.name2': 'Name 2',
  'erpProducts.detail.source': 'Source',
  'erpProducts.detail.excelFile': 'Excel file',
  'erpProducts.detail.lastSync': 'Last sync',
  'erpProducts.detail.updated': 'Updated',
  'erpProducts.detail.comment': 'Comment',
  'erpProducts.progress.fullCatalog': 'Full ERP catalog sync',
  'erpProducts.progress.filtered': 'Filtered products sync',
  'erpProducts.progress.enrich': 'ERP enrichment (local products)',
  'erpProducts.snack.loadError': 'Error loading products',
  'erpProducts.snack.enrichStarted': 'ERP enrichment started…',
  'erpProducts.snack.syncCancelled': 'Sync cancelled',
  'erpProducts.snack.syncFailed': 'Failed to start ERP sync',
  'erpProducts.snack.productSyncOk': 'Sync OK — {name}',

  'erpChanges.title': 'ERP Changes',
  'erpChanges.subtitle': 'Track changes detected on EuroBrico webservice products',
  'erpChanges.enrichFromErp': 'Enrich from ERP',
  'erpChanges.syncing': 'Syncing…',
  'erpChanges.importExcel': 'Import Excel',
  'erpChanges.importing': 'Import…',
  'erpChanges.excelPlusSync': 'Excel + ERP Sync',
  'erpChanges.refresh': 'Refresh',
  'erpChanges.progressTitle': 'ERP enrichment in progress',
  'erpChanges.stat.filteredTotal': 'Filtered total',
  'erpChanges.stat.unreadPage': 'Unread (page)',
  'erpChanges.stat.lastSync': 'Last sync',
  'erpChanges.filter.unreadOnly': 'Unread only',
  'erpChanges.filter.allTypes': 'All types',
  'erpChanges.filter.created': 'Created',
  'erpChanges.filter.updated': 'Updated',
  'erpChanges.filter.price': 'Price',
  'erpChanges.filter.stock': 'Stock',
  'erpChanges.filter.deleted': 'Deleted',
  'erpChanges.filter.allValues': 'All values',
  'erpChanges.filter.bothValues': 'Before and After filled',
  'erpChanges.filter.cleared': 'Value cleared (→ —)',
  'erpChanges.filter.added': 'Value added (— →)',
  'erpChanges.searchPlaceholder': 'Search product, ref, EAN, value…',
  'erpChanges.markSelected': 'Mark selected',
  'erpChanges.deleteSelected': 'Delete selected',
  'erpChanges.markAllPage': 'Mark all (page)',
  'erpChanges.cleanupFalsePositives': 'Clean price false positives',
  'erpChanges.cleaning': 'Cleaning…',
  'erpChanges.reset': 'Reset',
  'erpChanges.loading': 'Loading changes…',
  'erpChanges.empty': 'No ERP change for these filters',
  'erpChanges.startSync': 'Start a sync',
  'erpChanges.selectAll': 'Select all',
  'erpChanges.col.date': 'Date',
  'erpChanges.col.type': 'Type',
  'erpChanges.col.product': 'Product',
  'erpChanges.col.field': 'Field',
  'erpChanges.col.before': 'Before',
  'erpChanges.col.after': 'After',
  'erpChanges.col.status': 'Status',
  'erpChanges.status.read': 'Read',
  'erpChanges.status.unread': 'Unread',
  'erpChanges.pageInfo': 'Page {page} / {totalPages}',
  'erpChanges.syncLogsTitle': 'Latest syncs',
  'erpChanges.col.started': 'Started',
  'erpChanges.col.new': 'New',
  'erpChanges.col.updated': 'Updated',
  'erpChanges.col.failed': 'Failed',
  'erpChanges.col.changes': 'Changes',
  'erpChanges.snack.loadError': 'Error loading ERP changes',
  'erpChanges.snack.selectAtLeastOne': 'Select at least one change',
  'erpChanges.snack.markedRead': '{count} change(s) marked as read',
  'erpChanges.snack.deleted': '{count} change(s) deleted',
  'erpChanges.snack.enrichStarted': 'ERP enrichment started…',
  'erpChanges.snack.importExcel': 'Excel import in progress…',
  'erpChanges.snack.importExcelSync': 'Excel import + ERP sync…',
  'erpChanges.snack.importFailed': 'Excel import failed',

  'common.save': 'Save',

  'common.saving': 'Saving…',

  'common.edit': 'Edit',

  'common.delete': 'Delete',

  'common.actions': 'Actions',

  'common.status': 'Status',

  'common.date': 'Date',

  'common.notes': 'Notes',

  'common.name': 'Name',

  'common.code': 'Code',

  'common.email': 'Email',

  'common.phone': 'Phone',

  'common.city': 'City',

  'common.address': 'Address',

  'common.postalCode': 'Postal code',

  'common.country': 'Country',

  'common.ht': 'excl. VAT',

  'common.vat': 'VAT',

  'common.ttc': 'incl. VAT',

  'common.qty': 'Qty',

  'common.description': 'Description',

  'common.ref': 'Ref',

  'common.totalHt': 'Total excl. VAT',

  'common.totalVat': 'Total VAT',

  'common.totalTtc': 'Total incl. VAT',

  'common.unitPriceHt': 'Unit price excl. VAT',

  'common.vatPercent': 'VAT %',

  'common.noLines': 'No lines.',

  'common.customer': 'Customer',

  'common.supplier': 'Supplier',

  'common.active': 'Active',

  'common.inactive': 'Inactive',

  'common.amountHt': 'Amount excl. VAT',

  'common.amountTtc': 'Amount incl. VAT',

  'common.error': 'Error.',

  'common.detail': 'Detail',

  'common.none': '— none —',

  'common.optionalNone': '-- None --',

  'common.notProvided': 'not provided',

  'common.noNumber': 'no number',

  'common.select': '-- Select --',

  'common.addLine': '+ Add a line',

  'common.addLineShort': '+ Line',

  'common.updated': 'updated',

  'common.created': 'created',

  'common.label': 'Label',



  'sales.title': 'Sales & Customers',

  'sales.subtitle': 'Quote → Order → Invoice → Payment / Credit Note',

  'sales.searchPlaceholder': 'Search (Customer, Ref, No.)...',

  'sales.btn.newInvoice': 'New Invoice',

  'sales.btn.newOrder': 'New Order',

  'sales.btn.newQuote': 'New Quote',

  'sales.btn.newDeliveryNote': 'New DN',

  'sales.btn.newCustomer': 'New Customer',

  'sales.tab.invoices': 'Customer Invoices',

  'sales.tab.orders': 'Customer Orders',

  'sales.tab.quotes': 'Quotes',

  'sales.tab.creditNotes': 'Customer Credit Notes',

  'sales.tab.deliveryNotes': 'Delivery Notes',

  'sales.tab.customers': 'Customer Directory',

  'sales.col.invoiceNumber': 'Invoice No.',

  'sales.col.orderNumber': 'Order No.',

  'sales.col.quoteNumber': 'Quote No.',

  'sales.col.creditNoteNumber': 'Credit Note No.',

  'sales.col.deliveryNumber': 'DN No.',

  'sales.col.dueDate': 'Due date',

  'sales.col.paid': 'Paid',

  'sales.col.expiration': 'Expiry',

  'sales.col.linkedInvoice': 'Linked invoice',

  'sales.col.order': 'Order',

  'sales.col.vatNumber': 'VAT',

  'sales.col.balance': 'Balance',

  'sales.col.delivered': 'Delivered',

  'sales.col.ordered': 'Ordered',

  'sales.col.orderedQty': 'Qty ordered',

  'sales.col.deliveredQty': 'Qty delivered',

  'sales.col.codeRef': 'Code / Ref',

  'sales.col.linkedOrder': 'Linked order',

  'sales.btn.pay': 'Pay',

  'sales.btn.createCreditNote': 'Create credit note',

  'sales.btn.createDeliveryNote': 'Create DN',

  'sales.btn.invoice': 'Invoice',

  'sales.btn.order': 'Order',

  'sales.btn.validate': 'Validate',

  'sales.btn.apply': 'Apply',

  'sales.btn.createInvoice': 'Create an invoice',

  'sales.btn.createOrder': 'Create an order',

  'sales.btn.createQuote': 'Create a quote',

  'sales.btn.createDeliveryNoteLink': 'Create a DN',

  'sales.btn.createCustomer': 'Create a customer',

  'sales.btn.newCustomerLink': '+ New customer',

  'sales.btn.validatePayment': 'Confirm Payment',

  'sales.btn.createCustomerSubmit': 'Create customer',

  'sales.empty.invoices': 'No invoices.',

  'sales.empty.orders': 'No orders.',

  'sales.empty.quotes': 'No quotes.',

  'sales.empty.creditNotes': 'No customer credit notes found.',

  'sales.empty.deliveryNotes': 'No delivery notes.',

  'sales.empty.customers': 'No customers.',

  'sales.customerHash': 'Customer #{id}',

  'sales.label.customer': 'Customer:',

  'sales.label.date': 'Date:',

  'sales.label.status': 'Status:',

  'sales.label.paid': 'Paid:',

  'sales.label.ht': 'excl. VAT:',

  'sales.label.vat': 'VAT:',

  'sales.label.ttc': 'incl. VAT:',

  'sales.label.delivered': 'Delivered: {qty}',

  'sales.label.ordered': 'Ordered: {qty}',

  'sales.selectCustomer': '-- Select a customer --',

  'sales.notesPlaceholder': 'Internal notes / terms',

  'sales.modal.editCustomer': 'Edit customer',

  'sales.modal.newCustomer': 'New Customer',

  'sales.modal.payment': 'Record a Payment',

  'sales.modal.newDeliveryNote': 'New Delivery Note',

  'sales.modal.newQuote': 'New Quote',

  'sales.modal.newOrder': 'New Order',

  'sales.modal.newInvoice': 'New Invoice',

  'sales.customer.codeAuto': 'Code (auto if empty)',

  'sales.customer.codePlaceholder': 'CUST-...',

  'sales.customer.nameRequired': 'Name *',

  'sales.customer.namePlaceholder': 'Company name / name',

  'sales.customer.vatNumber': 'VAT No.',

  'sales.customer.required': 'Customer *',

  'sales.payment.invoiceNumber': 'Invoice No.',

  'sales.payment.amount': 'Amount due:',

  'sales.payment.method': 'Payment method:',

  'sales.payment.cash': 'Cash',

  'sales.payment.card': 'Bank Card',

  'sales.payment.transfer': 'Bank Transfer',

  'sales.lines': 'Lines',

  'sales.needCustomerFirst': 'Create a customer first in the Customer Directory tab.',

  'sales.pdfDownloaded': 'PDF downloaded: {fileName}',

  'sales.pdfError': 'PDF download failed.',

  'sales.selectCustomerError': 'Select a customer.',

  'sales.addLineError': 'Add at least one line.',

  'sales.quoteCreated': 'Quote {number} created.',

  'sales.orderCreated': 'Order {number} created.',

  'sales.invoiceCreated': 'Invoice {number} created.',

  'sales.action.createQuote': 'quote creation',

  'sales.action.createOrder': 'order creation',

  'sales.action.createInvoice': 'invoice creation',

  'sales.confirm.deleteCustomer': 'Delete customer « {name} »?',

  'sales.customerDeleted': 'Customer {name} deleted.',

  'sales.customerDeleteError': 'Unable to delete this customer.',

  'sales.customerNameRequired': 'Customer name is required.',

  'sales.customerSaved': 'Customer {name} ({code}) {verb}.',

  'sales.customerSaveError': 'Error while {action} the customer.',

  'sales.customerUpdateAction': 'updating',

  'sales.customerCreateAction': 'creating',

  'sales.orderFromQuote': 'Order {order} created from quote {quote}.',

  'sales.quoteToOrderError': 'Quote → order conversion failed.',

  'sales.invoiceFromOrder': 'Invoice {invoice} created from order {order}.',

  'sales.orderToInvoiceError': 'Order → invoice conversion failed.',

  'sales.paymentSaved': 'Payment of {amount} € recorded.',

  'sales.paymentError': 'Payment failed.',

  'sales.creditNoteFromInvoice': 'Credit note {creditNote} created from invoice {invoice}.',

  'sales.creditNoteCreateError': 'Error while creating the credit note.',

  'sales.creditNoteValidated': 'Credit note {number} validated.',

  'sales.creditNoteValidateError': 'Error while validating the credit note.',

  'sales.creditNoteApplied': 'Credit note {number} applied.',

  'sales.creditNoteApplyError': 'Error while applying the credit note.',

  'sales.detailLoadError': 'Unable to load details.',

  'sales.detailTitle.quote': 'Quote {number}',

  'sales.detailTitle.order': 'Order {number}',

  'sales.detailTitle.invoice': 'Invoice {number}',

  'sales.detailTitle.creditNote': 'Credit note {number}',

  'sales.detailTitle.deliveryNote': 'DN {number}',

  'sales.errorDuring': 'Error during {action}.',

  'sales.deliveryNoteCreated': 'DN {number} created.',

  'sales.deliveryNoteCreateError': 'Error while creating the DN.',

  'sales.deliveryNoteFromOrder': 'DN {delivery} created from order {order}.',

  'sales.invoiceFromDeliveryNote': 'Invoice {invoice} created from DN {delivery}.',

  'sales.genericError': 'Error.',

  'sales.pdfGenericError': 'PDF error.',

  'sales.confirm.deleteDeliveryNote': 'Delete DN {number}?',

  'sales.deliveryNoteDeleted': 'DN {number} deleted.',

  'sales.deleteError': 'Delete error.',



  'purchases.title': 'Purchases & Suppliers',

  'purchases.subtitle': 'Track supplier orders and generate supplier invoices from parsed documents.',

  'purchases.searchPlaceholder': 'Search (supplier, number, note)...',

  'purchases.btn.parsedDocuments': 'Parsed documents',

  'purchases.btn.uploadDocument': 'Upload a document',

  'purchases.btn.newInvoice': 'New invoice',

  'purchases.btn.newOrder': 'New order',

  'purchases.btn.newSupplier': 'New supplier',

  'purchases.tab.supplierInvoices': 'Supplier invoices',

  'purchases.tab.purchaseOrders': 'Purchase orders',

  'purchases.tab.parsedDocuments': 'Parsed documents',

  'purchases.tab.receipts': 'Receipts',

  'purchases.tab.suppliers': 'Supplier Directory',

  'purchases.col.invoiceNumber': 'Invoice No.',

  'purchases.col.document': 'Document',

  'purchases.col.order': 'Order',

  'purchases.col.dueDate': 'Due date',

  'purchases.col.orderNumber': 'Order No.',

  'purchases.col.expectedDelivery': 'Expected delivery',

  'purchases.col.received': 'Received',

  'purchases.col.documentNumber': 'Document No.',

  'purchases.col.type': 'Type',

  'purchases.col.file': 'File',

  'purchases.col.documentDate': 'Document date',

  'purchases.col.addedAt': 'Added on',

  'purchases.col.target': 'Target',

  'purchases.col.receiptNumber': 'Receipt No.',

  'purchases.col.sourceDocument': 'Source document',

  'purchases.col.cfa': 'CFA',

  'purchases.col.lines': 'Lines',

  'purchases.col.qtyReceived': 'Qty received',

  'purchases.col.vatNumber': 'VAT',

  'purchases.btn.linkDocument': 'Link document',

  'purchases.btn.matchOrder': 'Match order',

  'purchases.btn.receiveDelivery': 'Receive DN',

  'purchases.btn.comptabiliser': 'Post',

  'purchases.btn.linkDocumentSubmit': 'Link document',

  'purchases.btn.match': 'Match',

  'purchases.btn.applyDelivery': 'Apply delivery note',

  'purchases.btn.createSupplier': 'Create supplier',

  'purchases.btn.newSupplierLink': '+ New supplier',

  'purchases.btn.saveInvoice': 'Save invoice',

  'purchases.btn.saveOrder': 'Save order',

  'purchases.empty.supplierInvoices': 'No supplier invoices found.',

  'purchases.empty.purchaseOrders': 'No purchase orders found.',

  'purchases.empty.parsedDocuments': 'No parsed documents (invoice or DN).',

  'purchases.empty.receipts': 'No receipts. Post a DN document.',

  'purchases.empty.suppliers': 'No suppliers. Create one to start the purchase cycle.',

  'purchases.hint.parsedDocuments': 'OCR documents (Documents). Posting creates a supplier invoice or a receipt (ErpReceipts).',

  'purchases.hint.receipts': 'Business receipts (ErpReceipts) created after posting a parsed DN.',

  'purchases.status.comptabilise': 'Posted',

  'purchases.status.pending': 'Pending',

  'purchases.type.invoice': 'Invoice',

  'purchases.type.deliveryNote': 'Delivery Note',

  'purchases.target.invoiceFo': 'Supplier invoice',

  'purchases.target.receipt': 'Receipt',

  'purchases.docHash': 'Doc #{id}',

  'purchases.supplierHash': '#{id}',

  'purchases.label.supplier': 'Supplier:',

  'purchases.label.date': 'Date:',

  'purchases.label.status': 'Status:',

  'purchases.label.document': 'Document:',

  'purchases.label.order': 'Order:',

  'purchases.label.expectedDelivery': 'Expected delivery:',

  'purchases.parsedHintPrefix': 'OCR documents (',

  'purchases.parsedHintMid': '). ',

  'purchases.parsedHintComptabiliser': 'Post',

  'purchases.parsedHintCreates': ' creates a ',

  'purchases.parsedHintOr': ' or a ',

  'purchases.modal.comptabiliserFromDoc': 'Post an invoice from a document',

  'purchases.modal.newSupplierInvoice': 'New supplier invoice',

  'purchases.modal.linkDocument': 'Link a document to the supplier invoice',

  'purchases.modal.newPurchaseOrder': 'New purchase order',

  'purchases.modal.editSupplier': 'Edit supplier',

  'purchases.modal.newSupplier': 'New supplier',

  'purchases.modal.matchOrder': 'Match supplier invoice to an order',

  'purchases.modal.receiveDelivery': 'Receive a delivery note',

  'purchases.modal.comptabiliserDoc': 'Post document',

  'purchases.selectSupplier': '-- Select a supplier --',

  'purchases.selectDocument': '-- Select a document --',

  'purchases.selectPurchaseOrder': '-- Select a purchase order --',

  'purchases.selectDeliveryNote': '-- Select a delivery note --',

  'purchases.defaultVat': 'Default VAT (%)',

  'purchases.detectedSupplier': 'Supplier detected on document:',

  'purchases.comptabilising': 'Posting…',

  'purchases.number': 'Number',

  'purchases.numberPlaceholder': 'Leave empty for auto-numbering',

  'purchases.notesPlaceholder': 'Optional comment',

  'purchases.invoiceLines': 'Invoice lines',

  'purchases.orderLines': 'Order lines',

  'purchases.invoiceLabel': 'Invoice:',

  'purchases.orderLabel': 'Order:',

  'purchases.match.balanced': 'Balanced match',

  'purchases.match.gaps': 'Discrepancies detected',

  'purchases.match.invoiceHt': 'Invoice total excl. VAT:',

  'purchases.match.orderHt': 'Order total excl. VAT:',

  'purchases.match.deltaHt': 'Delta excl. VAT:',

  'purchases.match.matchedLines': 'Matched lines:',

  'purchases.match.qtyGaps': 'Quantity discrepancies:',

  'purchases.match.priceGaps': 'Price discrepancies:',

  'purchases.match.moreWarnings': '{count} other discrepancy(ies) not shown.',

  'purchases.supplier.codeAuto': 'Code (auto if empty)',

  'purchases.supplier.codePlaceholder': 'SUP-...',

  'purchases.supplier.nameRequired': 'Name *',

  'purchases.supplier.namePlaceholder': 'Company name',

  'purchases.supplier.vatNumber': 'VAT No.',

  'purchases.supplier.required': 'Supplier *',

  'purchases.purchaseOrderOptional': 'Purchase order (optional)',

  'purchases.parsedDocument': 'Parsed document',

  'purchases.deliveryDocument': 'Parsed delivery note',

  'purchases.purchaseOrder': 'Purchase order',

  'purchases.hint.invoiceCreates': 'Creates a posted supplier invoice (Validated status).',

  'purchases.hint.deliveryCreates': 'Creates a receipt (ErpReceipts) and updates stock.',

  'purchases.autoCreated': 'Supplier invoice #{id} automatically created from parsing.',

  'purchases.needSupplierFirst': 'Create a supplier first.',

  'purchases.pdfDownloaded': 'PDF downloaded: {fileName}',

  'purchases.pdfError': 'PDF download failed.',

  'purchases.confirm.deleteSupplier': 'Delete supplier « {name} »?',

  'purchases.supplierDeleted': 'Supplier {name} deleted.',

  'purchases.selectSupplierError': 'Please select a supplier.',

  'purchases.selectSupplierAndDoc': 'Please select a supplier and a document.',

  'purchases.invoiceComptabilised': 'Invoice posted → {number}.{warnings}',

  'purchases.blComptabilised': 'DN posted → receipt {number} (ErpReceipts).',

  'purchases.stockAlreadyFed': 'Stock already updated (no duplicate entry).',

  'purchases.invoiceFromDoc': 'Invoice {number} posted from document.',

  'purchases.supplierInvoiceCreated': 'Supplier invoice {number} created.',

  'purchases.supplierInvoiceCreateError': 'Error while creating the supplier invoice.',

  'purchases.purchaseOrderCreated': 'Purchase order {number} created.',

  'purchases.purchaseOrderCreateError': 'Error while creating the purchase order.',

  'purchases.supplierSaved': 'Supplier {name} ({code}) {verb}.',

  'purchases.supplierSaveError': 'Error while {action} the supplier.',

  'purchases.supplierUpdateAction': 'updating',

  'purchases.supplierCreateAction': 'creating',

  'purchases.selectDocumentError': 'Please select a document.',

  'purchases.documentLinked': 'Document linked to supplier invoice.',

  'purchases.selectDeliveryError': 'Please select a delivery note.',

  'purchases.receiveDeliveryError': 'Error while receiving the delivery note.',

  'purchases.selectPurchaseOrderError': 'Please select a purchase order.',

  'purchases.matchPreviewError': 'Error while previewing the match.',

  'purchases.detailLoadError': 'Unable to load details.',

  'purchases.detailTitle.order': 'Order {number}',

  'purchases.detailTitle.invoice': 'Invoice {number}',

  'purchases.detailTitle.receipt': 'Receipt {number}',

  'purchases.addLineError': 'Add at least one line.',

  'purchases.supplierNameRequired': 'Supplier name is required.',

  'purchases.documentMissing': 'Document missing.',

  'purchases.comptabiliseError': 'Error while posting.',

  'purchases.supplierDeleteError': 'Unable to delete this supplier.',

  'purchases.linkDocumentError': 'Error while linking the document.',

  'purchases.matchError': 'Error while matching the supplier invoice.',

  'purchases.supplierNameHash': 'Supplier #{id}',

  'purchases.stockUpdated': 'Stock +{qty} ({count} movements).',

  'purchases.receiveStockEntry': 'Stock in: {qty} unit(s) across {count} product(s).',

  'purchases.receiveStockAlreadyFed': 'Stock already updated for this DN (no duplicate entry).',

  'purchases.matchOk': 'Match completed between invoice {invoice} and order {order}.',

  'purchases.matchWithGaps': 'Match completed with discrepancies between invoice {invoice} and order {order}.',

  'purchases.receiveApplied': 'DN receipt applied to order {order}.',



  'cash.title': 'Cash & Finance Module',

  'cash.subtitle': 'Open, operate and close the cash session.',

  'cash.btn.newOperation': 'New Operation',

  'cash.btn.close': 'Close Cash Register',

  'cash.btn.open': 'Open Cash Register',

  'cash.tab.active': 'Active session',

  'cash.tab.history': 'History',

  'cash.noSession.title': 'No cash session is currently open.',

  'cash.noSession.hint': 'Open a session to record cash sales and make deposits/withdrawals.',

  'cash.metric.sessionNumber': 'Session No.',

  'cash.metric.openingBalance': 'Opening Float',

  'cash.metric.theoretical': 'Theoretical balance',

  'cash.metric.inOut': 'Inflows / Outflows',

  'cash.metric.openedBy': 'Opened by',

  'cash.metric.status': 'Status',

  'cash.operationsTitle': 'Session Operations',

  'cash.col.date': 'Date',

  'cash.col.operationType': 'Operation Type',

  'cash.col.description': 'Description',

  'cash.col.reference': 'Reference',

  'cash.col.amount': 'Amount',

  'cash.col.author': 'Author',

  'cash.col.type': 'Type',

  'cash.col.number': 'No.',

  'cash.col.opening': 'Opening',

  'cash.col.status': 'Status',

  'cash.col.float': 'Float',

  'cash.col.closing': 'Closing',

  'cash.col.variance': 'Variance',

  'cash.empty.operations': 'No operations recorded in this session.',

  'cash.empty.sessions': 'No sessions recorded.',

  'cash.empty.historyOps': 'No operations.',

  'cash.historyTitle': 'Recent sessions',

  'cash.detailTitle': 'Detail — {number}',

  'cash.openedAt': 'Opened: {date} by {by}',

  'cash.closedAt': 'Closed: {date} by {by}',

  'cash.expected': 'Expected: {amount}',

  'cash.modal.open': 'Open Cash Register',

  'cash.modal.close': 'Close Cash Register',

  'cash.modal.newOp': 'New Cash Operation',

  'cash.openingBalanceLabel': 'Opening float (€):',

  'cash.confirmOpen': 'Confirm opening',

  'cash.theoreticalLabel': 'Theoretical balance:',

  'cash.realCountLabel': 'Actual cash count (€):',

  'cash.varianceLabel': 'Variance:',

  'cash.opTypeLabel': 'Operation type:',

  'cash.op.deposit': 'Cash deposit',

  'cash.op.withdrawal': 'Cash withdrawal',

  'cash.op.salePayment': 'Direct cash sale',

  'cash.amountLabel': 'Amount (€):',

  'cash.descriptionLabel': 'Description:',

  'cash.descriptionPlaceholder': 'Reason or reference',

  'cash.referenceLabel': 'Document reference:',

  'cash.referencePlaceholder': 'FAC-... / ticket',

  'cash.opLabel.deposit': 'Deposit',

  'cash.opLabel.withdrawal': 'Withdrawal',

  'cash.opLabel.salePayment': 'Cash sale',

  'cash.loadSessionError': 'Unable to load session.',

  'cash.opened': 'Cash session opened.',

  'cash.openError': 'Unable to open the cash register.',

  'cash.closed': 'Cash register closed. Variance: {diff} €.',

  'cash.closeError': 'Unable to close the cash register.',

  'cash.invalidAmount': 'Invalid amount.',

  'cash.opSaved': 'Operation recorded.',

  'cash.opSaveError': 'Unable to save the operation.',



  'numbering.title': 'Document numbering',

  'numbering.subtitle': 'Prefixes, formats and sequential counters (quotes, invoices, credit notes, purchases…).',

  'numbering.initDefaults': 'Initialise defaults',

  'numbering.placeholdersHint': 'Placeholders available in the format:',

  'numbering.example': 'Example:',

  'numbering.empty.title': 'No sequence configured',

  'numbering.empty.hint': 'Click « Initialise defaults » to create the business counters.',

  'numbering.prefix': 'Prefix',

  'numbering.year': 'Year',

  'numbering.nextNumber': 'Next No.',

  'numbering.format': 'Format',

  'numbering.type.Quote': 'Quote',

  'numbering.type.Order': 'Customer order',

  'numbering.type.Invoice': 'Customer invoice',

  'numbering.type.CreditNote': 'Customer credit note',

  'numbering.type.PurchaseOrder': 'Purchase order',

  'numbering.type.SupplierInvoice': 'Supplier invoice',

  'numbering.type.DeliveryNote': 'Delivery note',

  'numbering.loadError': 'Unable to load sequences.',

  'numbering.defaultsReady': 'Default sequences created / verified.',

  'numbering.initError': 'Unable to initialise sequences.',

  'numbering.prefixRequired': 'Prefix is required.',

  'numbering.nextNumberInvalid': 'Next number must be ≥ 1.',

  'numbering.formatRequired': 'Format is required.',

  'numbering.saved': 'Sequence {type} saved.',

  'numbering.saveError': 'Unable to save.',



  'admin.title': 'Administration',

  'admin.tab.tenants': 'Tenants',

  'admin.tab.companies': 'Companies',

  'admin.tab.roles': 'Roles',

  'admin.tab.users': 'Users',

  'admin.btn.newTenant': 'New Tenant',

  'admin.btn.newCompany': 'New Company',

  'admin.btn.newRole': 'New Role',

  'admin.btn.newUser': 'New user',

  'admin.col.name': 'Name',

  'admin.col.active': 'Active',

  'admin.col.companies': 'Companies',

  'admin.col.createdAt': 'Created on',

  'admin.col.createdAtF': 'Created on',

  'admin.col.language': 'Language',

  'admin.col.currency': 'Currency',

  'admin.col.permissions': 'Permissions',

  'admin.col.user': 'User',

  'admin.col.email': 'Email',

  'admin.col.role': 'Role',

  'admin.col.activeCompany': 'Active company',

  'admin.col.company': 'Company',

  'admin.col.id': 'ID',

  'admin.empty.tenants': 'No tenants.',

  'admin.empty.companies': 'No companies.',

  'admin.empty.companiesHint': 'No companies.',

  'admin.empty.createCompany': 'Create a company',

  'admin.empty.roles': 'No roles.',

  'admin.empty.users': 'No users.',

  'admin.empty.access': 'No access.',

  'admin.active': 'Active',

  'admin.inactive': 'Inactive',

  'admin.activeF': 'Active',

  'admin.inactiveF': 'Inactive',

  'admin.role.admin': 'Admin',

  'admin.role.user': 'User',

  'admin.companiesAccessible': 'Accessible companies:',

  'admin.modal.newRole': 'New role',

  'admin.modal.editRole': 'Edit role',

  'admin.modal.editTenant': 'Edit tenant',

  'admin.modal.newTenant': 'New Tenant',

  'admin.modal.editCompany': 'Edit company',

  'admin.modal.newCompany': 'New Company',

  'admin.modal.editUser': 'Edit user',

  'admin.modal.newUser': 'New user',

  'admin.modal.resetPassword': 'Reset password',

  'admin.modal.assign': 'Assign {username} to a company',

  'admin.role.nameRequired': 'Role name *',

  'admin.role.allPermissions': 'All permissions',

  'admin.role.adminLocked': 'The Admin role automatically has all permissions (not editable).',

  'admin.role.namePlaceholder': 'E.g.: Accountant, Sales…',

  'admin.permissions': 'Permissions',

  'admin.tenant.namePlaceholder': 'Tenant name',

  'admin.company.namePlaceholder': 'Company name',

  'admin.company.tenantRequired': 'Tenant *',

  'admin.user.usernameRequired': 'Username *',

  'admin.user.usernamePlaceholder': 'Login / email',

  'admin.user.emailPlaceholder': 'email@domain.com',

  'admin.user.firstName': 'First name',

  'admin.user.lastName': 'Last name',

  'admin.user.lastNamePlaceholder': 'Family name',

  'admin.user.passwordRequired': 'Password *',

  'admin.user.passwordOptional': 'New password (leave blank = unchanged)',

  'admin.user.defaultCompany': 'Default company',

  'admin.user.businessRole': 'Business role',

  'admin.user.standardRole': '— Standard user —',

  'admin.user.isAdmin': 'Administrator',

  'admin.user.label': 'User:',

  'admin.user.newPassword': 'New password *',

  'admin.user.newPasswordPlaceholder': 'New password',

  'admin.resetting': 'Resetting…',

  'admin.reset': 'Reset',

  'admin.assigning': 'Assigning…',

  'admin.assign': 'Assign',

  'admin.nameRequired': 'Name *',

  'admin.title.edit': 'Edit',

  'admin.title.delete': 'Delete',

  'admin.title.editPermissions': 'Edit / permissions',

  'admin.title.resetPassword': 'Reset password',

  'admin.title.assignCompany': 'Assign company',

  'admin.title.remove': 'Remove',

  'admin.error.nameRequired': 'Name required.',

  'admin.error.tenantRequired': 'Tenant required.',

  'admin.error.usernameRequired': 'Username required.',

  'admin.error.passwordRequired': 'Password required.',

  'admin.error.roleNameRequired': 'Role name required.',

  'admin.tenantSaved': 'Tenant "{name}" saved.',

  'admin.companySaved': 'Company "{name}" saved.',

  'admin.userAssigned': 'User assigned to company.',

  'admin.confirm.removeAccess': 'Remove this user from the company?',

  'admin.accessRemoved': 'Access removed.',

  'admin.userUpdated': 'User updated.',

  'admin.userCreated': 'User created.',

  'admin.confirm.deleteUser': 'Delete user "{username}"?',

  'admin.userDeleted': 'User "{username}" deleted.',

  'admin.passwordReset': 'Password reset for "{username}".',

  'admin.roleUpdated': 'Role updated.',

  'admin.roleCreated': 'Role created.',

  'admin.confirm.deleteRole': 'Delete role "{name}"?',

  'admin.roleDeleted': 'Role "{name}" deleted.',

  'admin.perm.cat.sales': 'Sales',

  'admin.perm.cat.purchases': 'Purchases',

  'admin.perm.cat.stock': 'Stock',

  'admin.perm.cat.erp': 'ERP Products',

  'admin.perm.cat.documents': 'Documents',

  'admin.perm.cat.cash': 'Cash',

  'admin.perm.cat.settings': 'Settings',

  'admin.perm.cat.admin': 'Administration',

  'admin.perm.sec.customers': 'Customers',

  'admin.perm.sec.quotes': 'Quotes',

  'admin.perm.sec.orders': 'Orders',

  'admin.perm.sec.deliveryNotes': 'Delivery notes',

  'admin.perm.sec.invoices': 'Invoices',

  'admin.perm.sec.suppliers': 'Suppliers',

  'admin.perm.sec.purchaseOrders': 'Purchase orders',

  'admin.perm.sec.receipts': 'Receipts',

  'admin.perm.sec.supplierInvoices': 'Supplier invoices',

  'admin.perm.sec.stock': 'Stock',

  'admin.perm.sec.products': 'Product catalogue',

  'admin.perm.sec.erpChanges': 'ERP changes',

  'admin.perm.sec.documents': 'Documents',

  'admin.perm.sec.cash': 'Cash',

  'admin.perm.sec.numbering': 'Numbering',

  'admin.perm.sec.users': 'Users',

  'admin.perm.sec.roles': 'Roles',

  'admin.perm.action.Read': 'Read',

  'admin.perm.action.Create': 'Create',

  'admin.perm.action.Update': 'Update',

  'admin.perm.action.Delete': 'Delete',

  'admin.perm.action.Manage': 'Manage',

  'admin.perm.action.Upload': 'Upload',

  'admin.perm.action.Link': 'Link',

  'assistant.title': 'Store assistant',
  'assistant.subtitle': 'Product advice, quote and order',
  'assistant.project': 'Project',
  'assistant.budget': 'Budget',
  'assistant.cart': 'Cart',
  'assistant.cartEmpty': 'No products in the cart.',
  'assistant.close': 'Close',
  'assistant.remove': 'Remove',
  'assistant.welcome': 'Hello! I am the store assistant. Ask for a product, brand or project (paint, electrical…).',
  'assistant.redirecting': 'Redirecting to the store assistant…',
  'assistant.placeholder': 'E.g. white wall paint 10L, LED bulb, drill…',
  'assistant.send': 'Send',
  'assistant.quote': 'Request quote',
  'assistant.order': 'Order',
  'assistant.downloadQuote': 'Download quote',
  'assistant.downloadInvoice': 'Download invoice',
  'assistant.payCard': 'Pay by card',
  'assistant.product': 'Product',
  'assistant.price': 'Price',
  'assistant.qty': 'Qty',
  'assistant.error': 'Sorry, something went wrong. Please try again.',
  'assistant.newProject': 'New project',
  'assistant.photo': 'Attach a photo',
  'assistant.listening': 'Listening…',
  'assistant.lang': 'Language',
  'assistant.nextStep': 'Next step',
  'assistant.next': 'Next',
  'assistant.reviewCart': 'Review cart',
  'assistant.langSwitched': 'Language: English.'
};

const DICT: Record<AppLang, Dict> = { fr: FR, nl: NL, en: EN };

@Injectable({ providedIn: 'root' })
export class AppI18nService {
  readonly languages: { code: AppLang; label: string }[] = [
    { code: 'fr', label: 'FR' },
    { code: 'nl', label: 'NL' },
    { code: 'en', label: 'EN' }
  ];

  private readonly langSubject = new BehaviorSubject<AppLang>(this.readStored());
  readonly lang$ = this.langSubject.asObservable();

  get lang(): AppLang {
    return this.langSubject.value;
  }

  setLang(lang: AppLang): void {
    this.langSubject.next(lang);
    try {
      localStorage.setItem(STORAGE_KEY, lang);
    } catch {
      /* ignore */
    }
  }

  t(key: string, params?: Record<string, string | number>): string {
    let text = DICT[this.lang][key] ?? DICT.fr[key] ?? key;
    if (params) {
      for (const [k, v] of Object.entries(params)) {
        text = text.replace(new RegExp(`\\{${k}\\}`, 'g'), String(v));
      }
    }
    return text;
  }

  /** Resolve legacy assistant keys (without prefix) for gradual migration. */
  ta(key: string, params?: Record<string, string | number>): string {
    return this.t(key.includes('.') ? key : `assistant.${key}`, params);
  }

  speechLocale(): string {
    switch (this.lang) {
      case 'nl': return 'nl-BE';
      case 'en': return 'en-GB';
      default: return 'fr-FR';
    }
  }

  numberLocale(): string {
    switch (this.lang) {
      case 'nl': return 'nl-BE';
      case 'en': return 'en-GB';
      default: return 'fr-BE';
    }
  }

  private readStored(): AppLang {
    try {
      const raw = (
        localStorage.getItem(STORAGE_KEY)
        || localStorage.getItem(LEGACY_STORAGE_KEY)
        || 'fr'
      ).toLowerCase();
      if (raw === 'nl' || raw === 'en' || raw === 'fr') return raw;
    } catch {
      /* ignore */
    }
    return 'fr';
  }
}
