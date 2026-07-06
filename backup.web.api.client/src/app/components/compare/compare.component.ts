import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { DocumentService } from '../../services/document.service';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MaterialModule } from '../../material.module';
import { Document } from '../../models/document';
import { DocumentRelation } from '../../models/relation';
import { ComparisonResult, ErpPriceDiffLine, InvoicePriceComparisonResult } from '../../models/comparison';

@Component({
  selector: 'app-compare',
  templateUrl: './compare.component.html',
  styleUrls: ['./compare.component.css'],
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, RouterModule]
})
export class CompareComponent implements OnInit {
  documents: Document[] = [];
  factures: Document[] = [];
  bonsLivraison: Document[] = [];
  /** Types non reconnus comme facture ni BL (ex. ancienne donnée, libellé NL) */
  autresDocuments: Document[] = [];
  relations: DocumentRelation[] = [];
  relationMap: Record<number, string> = {};
  invoiceToDeliveriesMap: Record<number, Document[]> = {}; // Map facture -> BL associés
  batchStatusMap: Record<number, { updated: number; total: number }> = {};
  
  // Paramètres depuis la route (pour un BL uploadé)
  blId: number | null = null;
  blNumber: string | null = null;
  supplier: string | null = null;
  suggestedInvoices: Document[] = [];
  
  selectedInvoice: Document | null = null;
  selectedDelivery: Document | null = null;
  selectedInvoice1: Document | null = null; // Pour comparaison facture vs facture
  selectedInvoice2: Document | null = null; // Pour comparaison facture vs facture
  comparaisonResult: ComparisonResult | null = null;
  invoicePriceComparisonResult: InvoicePriceComparisonResult | null = null;
  erpPriceComparisonResult: ErpPriceDiffLine[] | null = null;
  erpPriceComparisonInvoice: Document | null = null;
  erpPriceComparisonLoading = false;
  
  expandedInvoiceId: number | null = null;

  constructor(
    private docs: DocumentService,
    private snack: MatSnackBar,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    // Récupérer les paramètres de la route
    this.route.queryParams.subscribe(params => {
      this.blId = params['blId'] ? parseInt(params['blId']) : null;
      this.blNumber = params['blNumber'] || null;
      this.supplier = params['supplier'] || null;
      
      this.load();
      this.loadRelations();
      
      // Si un BL est spécifié, rechercher les factures correspondantes
      if (this.blId && this.blNumber) {
        this.searchMatchingInvoices(this.blNumber);
      }
    });
  }

  load() {
    this.docs.list().subscribe(d => {
      this.documents = d;
      this.partitionDocuments(d);
      this.applySupplierFilterIfNeeded();
      
      // Si on arrive depuis l'upload avec un blId, sélectionner automatiquement le BL
      if (this.blId && !this.selectedDelivery) {
        const bl = this.bonsLivraison.find(b => b.id === this.blId);
        if (bl) {
          this.selectedDelivery = bl;
        }
      }
      
      // Recharger les relations pour mettre à jour la map
      this.loadRelations();
    });
  }

  loadRelations() {
    this.docs.relations().subscribe(rels => {
      this.relations = rels;
      const map: Record<number, string> = {};
      const invoiceToDeliveries: Record<number, Document[]> = {};
      
      for (const r of rels) {
        const current = invoiceToDeliveries[r.invoiceId] ?? [];
        map[r.invoiceId] = `→ ${current.length + 1} BL`;
        map[r.deliveryId] = `→ Facture #${r.invoiceId}`;
        
        // Trouver le BL associé à cette facture
        const delivery = this.bonsLivraison.find(bl => bl.id === r.deliveryId);
        if (delivery) {
          invoiceToDeliveries[r.invoiceId] = [...current, delivery];
        }
      }
      
      this.relationMap = map;
      this.invoiceToDeliveriesMap = invoiceToDeliveries;
    });
  }

  searchMatchingInvoices(blNumber: string) {
    this.docs.findInvoicesByBlNumber(blNumber).subscribe({
      next: (invoices) => {
        this.suggestedInvoices = invoices;
        if (invoices.length > 0) {
          this.snack.open(
            `${invoices.length} facture(s) trouvée(s) avec le numéro "${blNumber}"`,
            'OK',
            { duration: 3000 }
          );
        }
      },
      error: (err) => {
        console.error('Erreur lors de la recherche de factures:', err);
      }
    });
  }

  private applySupplierFilterIfNeeded() {
    if (!this.supplier) return;
    this.factures = this.factures.filter(f => f.supplier === this.supplier);
    this.bonsLivraison = this.bonsLivraison.filter(b => b.supplier === this.supplier);
    this.autresDocuments = this.autresDocuments.filter(x => x.supplier === this.supplier);
  }

  /** BL / livraison : testé en premier pour éviter les faux positifs « facture » dans le texte. */
  private isBonLivraisonDoc(d: Document): boolean {
    const t = (d.typeDocument ?? '').trim().toLowerCase();
    if (!t) return false;
    return (
      t === 'bonlivraison' ||
      t.includes('bon de livraison') ||
      t.includes('leveringsbon') ||
      t.includes('leveringsbevestiging') ||
      t.includes('delivery note') ||
      (t.includes('delivery') && t.includes('confirmation')) ||
      (t.includes('bon') && t.includes('livraison'))
    );
  }

  private isFactureDoc(d: Document): boolean {
    const t = (d.typeDocument ?? '').trim().toLowerCase();
    if (!t) return false;
    if (this.isBonLivraisonDoc(d)) return false;
    return (
      t === 'facture' ||
      t === 'factuur' ||
      t.includes('facture') ||
      t.includes('factuur') ||
      t.includes('invoice')
    );
  }

  private partitionDocuments(all: Document[]) {
    this.bonsLivraison = all.filter(d => this.isBonLivraisonDoc(d));
    this.factures = all.filter(d => this.isFactureDoc(d));
    this.autresDocuments = all.filter(d => !this.isBonLivraisonDoc(d) && !this.isFactureDoc(d));
  }

  selectInvoice(invoice: Document) {
    this.selectedInvoice = invoice;
    
    // Si on arrive depuis l'upload avec un blId, sélectionner automatiquement le BL
    if (this.blId && !this.selectedDelivery) {
      const bl = this.bonsLivraison.find(b => b.id === this.blId);
      if (bl) {
        this.selectedDelivery = bl;
      }
    }
    
    // Si un BL est déjà sélectionné, proposer l'association
    if (this.selectedDelivery) {
      // Ne pas appeler automatiquement linkSelected(), laisser l'utilisateur cliquer
    }
  }

  selectDelivery(delivery: Document) {
    this.selectedDelivery = delivery;
    // Si une facture est déjà sélectionnée, proposer l'association
    if (this.selectedInvoice) {
      this.linkSelected();
    }
  }

  linkSelected() {
    if (!this.selectedInvoice || !this.selectedDelivery) {
      this.snack.open('Veuillez sélectionner une facture et un BL', 'OK', { duration: 2000 });
      return;
    }
    
    this.docs.link(this.selectedInvoice.id, this.selectedDelivery.id).subscribe({
      next: () => {
        this.snack.open('Relation créée avec succès', 'OK', { duration: 2000 });
        this.loadRelations();
        this.clearPair();
        // Si on venait d'un upload, rediriger vers la page d'upload
        if (this.blId) {
          setTimeout(() => {
            this.router.navigate(['/upload']);
          }, 1500);
        }
      },
      error: (e) => {
        console.error(e);
        this.snack.open('Erreur lors de la création de la relation', 'Fermer', { duration: 3000 });
      }
    });
  }

  clearPair() {
    this.selectedInvoice = null;
    this.selectedDelivery = null;
  }

  compare(invoiceId: number, deliveryId: number) {
    this.docs.compare(invoiceId, deliveryId).subscribe({
      next: (res) => {
        console.log('Comparaison result:', res);
        if (res.lines && res.lines.length > 0) {
          console.log('First line:', res.lines[0]);
          console.log('First line actualQuantity:', res.lines[0].actualQuantity);
          console.log('First line isValidated:', res.lines[0].isValidated);
          console.log('First line productKey:', res.lines[0].productKey);
        }
        this.comparaisonResult = res;
        this.invoicePriceComparisonResult = null;
        this.erpPriceComparisonResult = null;
        this.snack.open('Comparaison effectuée', 'OK', { duration: 2000 });
      },
      error: (err) => {
        console.error('Erreur lors de la comparaison:', err);
        this.snack.open('Erreur lors de la comparaison', 'OK', { duration: 3000 });
      }
    });
  }

  compareAllDeliveries(invoiceId: number) {
    this.docs.compareAllDeliveries(invoiceId).subscribe({
      next: (res) => {
        this.comparaisonResult = res;
        this.selectedInvoice = this.factures.find(f => f.id === invoiceId) || null;
        this.selectedDelivery = null;
        this.invoicePriceComparisonResult = null;
        this.erpPriceComparisonResult = null;
        this.snack.open('Comparaison globale facture vs total BL effectuée', 'OK', { duration: 2500 });
      },
      error: (err) => {
        console.error('Erreur lors de la comparaison globale:', err);
        this.snack.open('Erreur lors de la comparaison globale', 'OK', { duration: 3000 });
      }
    });
  }

  onActualQuantityChange(line: any, event: any) {
    const value = event.target?.value;
    if (value === '' || value === null || value === undefined) {
      line.actualQuantity = null;
    } else {
      const numValue = Number(value);
      line.actualQuantity = isNaN(numValue) ? null : numValue;
    }
  }

  saveAdjustment(line: any) {
    if (!this.comparaisonResult || !this.selectedInvoice || !this.selectedDelivery) {
      return;
    }

    // Si la quantité a été modifiée après validation, réinitialiser la validation
    const shouldResetValidation = line.isValidated && line.actualQuantity !== null && line.actualQuantity !== line.deliveryQty;

    // Sauvegarder l'ajustement sans validation (ou réinitialiser la validation si la quantité a changé)
    this.docs.saveAdjustment({
      deliveryId: this.comparaisonResult.deliveryId,
      invoiceId: this.comparaisonResult.invoiceId,
      documentLineId: line.documentLineId ?? null,
      productKey: line.productKey,
      deliveryQuantity: line.deliveryQty,
      actualQuantity: line.actualQuantity ?? null,
      validate: false
    }).subscribe({
      next: (response) => {
        // Si la validation a été réinitialisée, mettre à jour le flag
        if (shouldResetValidation && response && !response.isValidated) {
          line.isValidated = false;
        }
      },
      error: (err) => {
        console.error('Erreur lors de la sauvegarde de l\'ajustement:', err);
        this.snack.open('Erreur lors de la sauvegarde', 'OK', { duration: 2000 });
      }
    });
  }

  validateAdjustment(line: any) {
    console.log('validateAdjustment called with line:', line);
    
    if (!this.comparaisonResult) {
      console.error('comparaisonResult is null');
      this.snack.open('Erreur: Aucune comparaison en cours', 'OK', { duration: 2000 });
      return;
    }
    if (!this.comparaisonResult.deliveryId || this.comparaisonResult.deliveryId <= 0) {
      this.snack.open('Validation ligne indisponible en comparaison globale', 'OK', { duration: 2500 });
      return;
    }

    if (!line.productKey) {
      console.error('productKey is missing:', line);
      this.snack.open('Erreur: Clé produit manquante', 'OK', { duration: 2000 });
      return;
    }

    // Vérifier que actualQuantity est défini et valide
    const actualQty = line.actualQuantity;
    if (actualQty == null || actualQty === undefined || actualQty === '') {
      console.error('actualQuantity is null/undefined/empty:', actualQty);
      this.snack.open('Veuillez saisir une quantité réelle', 'OK', { duration: 2000 });
      return;
    }

    const request = {
      deliveryId: this.comparaisonResult.deliveryId,
      invoiceId: this.comparaisonResult.invoiceId,
      documentLineId: line.documentLineId ?? null,
      productKey: line.productKey,
      deliveryQuantity: line.deliveryQty,
      actualQuantity: parseFloat(actualQty),
      validate: true
    };

    console.log('Sending validation request:', request);

    // Sauvegarder et valider l'ajustement
    this.docs.saveAdjustment(request).subscribe({
        next: (response) => {
          console.log('Validation response:', response);
          line.isValidated = true;
          const message = line.stockUpdated 
            ? 'Quantité réelle validée. Cliquez sur "Mettre à jour le stock (correction)" pour appliquer la correction.'
            : 'Quantité réelle validée avec succès';
          this.snack.open(message, 'OK', { duration: 4000 });
          // Recharger la comparaison pour mettre à jour les différences et recharger les ajustements
          setTimeout(() => {
            this.compare(this.comparaisonResult!.invoiceId, this.comparaisonResult!.deliveryId);
          }, 500);
        },
      error: (err) => {
        console.error('Erreur lors de la validation de l\'ajustement:', err);
        const errorMessage = err.error?.message || err.message || 'Erreur lors de la validation';
        this.snack.open(errorMessage, 'OK', { duration: 3000 });
      }
    });
  }

  compareOld(invoiceId: number, deliveryId: number) {
    this.docs.compare(invoiceId, deliveryId).subscribe(res => {
      this.comparaisonResult = res;
      this.invoicePriceComparisonResult = null; // Clear invoice comparison
      this.snack.open('Comparaison effectuée', 'OK', { duration: 2000 });
    });
  }

  compareWithErp(invoiceId: number) {
    this.erpPriceComparisonLoading = true;
    this.erpPriceComparisonResult = null;
    this.comparaisonResult = null;
    this.invoicePriceComparisonResult = null;
    this.erpPriceComparisonInvoice = this.factures.find(f => f.id === invoiceId) || null;

    this.docs.getErpPriceDiff(invoiceId).subscribe({
      next: (res) => {
        this.erpPriceComparisonResult = res;
        this.erpPriceComparisonLoading = false;
        this.snack.open('Comparaison avec les prix ERP effectuée', 'OK', { duration: 2500 });
      },
      error: (err) => {
        console.error('Erreur lors de la comparaison ERP:', err);
        this.erpPriceComparisonLoading = false;
        const errorMessage = err.error?.message || err.message || 'Erreur lors de la comparaison ERP';
        this.snack.open(errorMessage, 'Fermer', { duration: 4000 });
      }
    });
  }

  compareInvoices() {
    if (!this.selectedInvoice1 || !this.selectedInvoice2) {
      this.snack.open('Veuillez sélectionner deux factures', 'OK', { duration: 2000 });
      return;
    }

    // Vérifier que les deux factures ont le même fournisseur
    if (this.selectedInvoice1.supplier && this.selectedInvoice2.supplier) {
      if (this.selectedInvoice1.supplier.toLowerCase() !== this.selectedInvoice2.supplier.toLowerCase()) {
        this.snack.open(
          `Impossible de comparer deux factures de fournisseurs différents: "${this.selectedInvoice1.supplier}" et "${this.selectedInvoice2.supplier}"`,
          'OK',
          { duration: 4000 }
        );
        return;
      }
    }

    this.docs.compareInvoices(this.selectedInvoice1.id, this.selectedInvoice2.id).subscribe({
      next: (res) => {
        this.invoicePriceComparisonResult = res;
        this.comparaisonResult = null;
        this.erpPriceComparisonResult = null;
        this.snack.open('Comparaison de prix effectuée', 'OK', { duration: 2000 });
      },
      error: (err) => {
        console.error('Erreur lors de la comparaison:', err);
        const errorMessage = err.error?.message || err.message || 'Erreur lors de la comparaison';
        this.snack.open(errorMessage, 'OK', { duration: 4000 });
      }
    });
  }

  compareAndStock(invoiceId: number, deliveryId: number, forceUpdate: boolean = false) {
    this.docs.compareAndStock(invoiceId, deliveryId, forceUpdate).subscribe({
      next: (r) => {
        if (r.success) {
          const message = forceUpdate 
            ? 'Stock mis à jour avec les quantités corrigées' 
            : 'Stock mis à jour (quantités livrées)';
          this.snack.open(message, 'OK', { duration: 2500 });
          // Recharger la comparaison pour mettre à jour le statut stockUpdated
          setTimeout(() => {
            this.compare(invoiceId, deliveryId);
          }, 500);
        } else {
          this.snack.open('Différences détectées: stock non mis à jour', 'OK', { duration: 3000 });
        }
      },
      error: (e) => {
        console.error(e);
        const errorMessage = e.error?.message || 'Erreur lors de l\'alimentation du stock';
        this.snack.open(errorMessage, 'Fermer', { duration: 3000 });
      }
    });
  }

  compareAndStockAllDeliveries(invoiceId: number, forceUpdate: boolean = false) {
    this.docs.compareAndStockAllDeliveries(invoiceId, forceUpdate).subscribe({
      next: (r) => {
        if (r.totalDeliveries === 0) {
          this.snack.open('Aucun BL associé à cette facture', 'OK', { duration: 3000 });
          return;
        }

        const actionLabel = forceUpdate ? 'correction stock' : 'comparaison + stock';
        this.batchStatusMap[invoiceId] = { updated: r.updatedDeliveries, total: r.totalDeliveries };
        this.snack.open(
          `${actionLabel}: ${r.updatedDeliveries}/${r.totalDeliveries} BL traités`,
          'OK',
          { duration: 3500 }
        );

        this.loadRelations();
        if (this.comparaisonResult && this.comparaisonResult.invoiceId === invoiceId && this.comparaisonResult.deliveryId > 0) {
          this.compare(this.comparaisonResult.invoiceId, this.comparaisonResult.deliveryId);
        }
      },
      error: (e) => {
        console.error(e);
        const errorMessage = e.error?.message || 'Erreur lors du traitement des BL';
        this.snack.open(errorMessage, 'Fermer', { duration: 3000 });
      }
    });
  }

  getBatchStatus(invoiceId: number): { updated: number; total: number } | null {
    return this.batchStatusMap[invoiceId] ?? null;
  }

  reparseDocument(documentId: number) {
    if (!documentId) return;
    this.docs.reparseLines(documentId, false).subscribe({
      next: (r) => {
        if (r.success) {
          this.snack.open('Document re-parsé avec succès', 'OK', { duration: 2500 });
          // Recharger les documents pour voir les changements
          this.load();
          // Recharger la comparaison affichée si applicable
          if (this.comparaisonResult?.invoiceId && this.comparaisonResult?.deliveryId) {
            setTimeout(() => {
              this.compare(this.comparaisonResult!.invoiceId, this.comparaisonResult!.deliveryId);
            }, 500);
          } else if (this.selectedInvoice && this.selectedDelivery) {
            setTimeout(() => {
              this.compare(this.selectedInvoice!.id, this.selectedDelivery!.id);
            }, 500);
          }
        } else {
          this.snack.open('Erreur lors du re-parsing', 'Fermer', { duration: 3000 });
        }
      },
      error: (e) => {
        console.error(e);
        const errorMessage = e.error?.message || 'Erreur lors du re-parsing';
        this.snack.open(errorMessage, 'Fermer', { duration: 3000 });
      }
    });
  }

  hasCorrectedQuantities(): boolean {
    if (!this.comparaisonResult || !this.comparaisonResult.lines) {
      return false;
    }
    return this.comparaisonResult.lines.some(line => 
      line.stockUpdated && line.isValidated && line.actualQuantity !== null && line.actualQuantity !== line.deliveryQty
    );
  }

  hasQuantityChanged(line: any): boolean {
    // Si la ligne n'est pas validée, elle n'a pas changé (elle n'a jamais été validée)
    if (!line.isValidated) {
      return false;
    }
    // Si elle est validée mais que actualQuantity est null, elle a été réinitialisée
    if (line.actualQuantity === null) {
      return true;
    }
    // Si elle est validée et que actualQuantity != deliveryQty, c'est qu'elle a été modifiée après validation
    // (car si elle était validée avec deliveryQty, actualQuantity serait égal à deliveryQty)
    // Mais en fait, si on valide 15 alors que deliveryQty est 20, actualQuantity sera 15
    // Donc si isValidated est true et actualQuantity != deliveryQty, c'est qu'elle a été validée avec une valeur différente
    // Et si on la modifie ensuite, elle reste != deliveryQty mais peut être différente de la valeur validée originale
    // Pour simplifier : si isValidated est true et actualQuantity != deliveryQty, on permet la re-validation
    // car cela signifie qu'elle a été modifiée après validation
    return line.actualQuantity !== line.deliveryQty;
  }

  unlink(relationId: number) {
    if (!confirm('Confirmer la dissociation ?')) return;
    this.docs.unlink(relationId).subscribe(() => {
      this.loadRelations();
      this.load();
    });
  }

  download(id: number) {
    this.docs.download(id).subscribe(blob => {
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'document.pdf';
      a.click();
      window.URL.revokeObjectURL(url);
    });
  }

  navigateToUpload() {
    this.router.navigate(['/upload']);
  }

  toggleExpand(invoiceId: number) {
    if (this.expandedInvoiceId === invoiceId) {
      this.expandedInvoiceId = null;
    } else {
      this.expandedInvoiceId = invoiceId;
    }
  }

  isExpanded(invoiceId: number): boolean {
    return this.expandedInvoiceId === invoiceId;
  }

  getAssociatedDeliveries(invoiceId: number): Document[] {
    return this.invoiceToDeliveriesMap[invoiceId] || [];
  }

  getRelationId(invoiceId: number, deliveryId: number): number | null {
    const relation = this.relations.find(r => r.invoiceId === invoiceId && r.deliveryId === deliveryId);
    return relation ? relation.id : null;
  }

  /**
   * Retourne les BL disponibles pour association avec une facture donnée.
   * Filtre par fournisseur : seuls les BL du même fournisseur que la facture sont retournés.
   */
  getAvailableDeliveries(invoice: Document): Document[] {
    if (!invoice || !invoice.supplier) {
      return [];
    }
    
    // Filtrer les BL du même fournisseur que la facture
    return this.bonsLivraison.filter(bl => {
      // Vérifier que le BL a le même fournisseur
      if (!bl.supplier || bl.supplier !== invoice.supplier) {
        return false;
      }
      
      // Exclure les BL déjà associés à cette facture ou à une autre facture
      const isLinkedToThisInvoice = this.relations.some(r => r.deliveryId === bl.id && r.invoiceId === invoice.id);
      const isLinkedToAnotherInvoice = this.relations.some(r => r.deliveryId === bl.id && r.invoiceId !== invoice.id);
      return !isLinkedToThisInvoice && !isLinkedToAnotherInvoice;
    });
  }

  /**
   * Vérifie si deux factures peuvent être comparées (même fournisseur requis)
   */
  canCompareInvoices(): boolean {
    if (!this.selectedInvoice1 || !this.selectedInvoice2) {
      return false;
    }
    
    if (this.selectedInvoice1.supplier && this.selectedInvoice2.supplier) {
      return this.selectedInvoice1.supplier.toLowerCase() === this.selectedInvoice2.supplier.toLowerCase();
    }
    
    if (this.selectedInvoice1.supplier || this.selectedInvoice2.supplier) {
      return false;
    }
    
    return true;
  }

  getMatchCount(): number {
    return this.comparaisonResult?.lines?.filter(l => l.status === 'OK').length ?? 0;
  }

  getErrorCount(): number {
    return this.comparaisonResult?.lines?.filter(l => l.status !== 'OK').length ?? 0;
  }
}

