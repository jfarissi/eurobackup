import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { DocumentService } from '../../services/document.service';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MaterialModule } from '../../material.module';
import { Document } from '../../models/document';
import { DocumentRelation } from '../../models/relation';
import { ComparisonResult, InvoicePriceComparisonResult } from '../../models/comparison';

@Component({
  selector: 'app-compare',
  templateUrl: './compare.component.html',
  styleUrls: ['./compare.component.css'],
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule]
})
export class CompareComponent implements OnInit {
  documents: Document[] = [];
  factures: Document[] = [];
  bonsLivraison: Document[] = [];
  relations: DocumentRelation[] = [];
  relationMap: Record<number, string> = {};
  invoiceToDeliveryMap: Record<number, Document> = {}; // Map facture -> BL associé
  
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
  
  query = '';
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
      this.factures = d.filter(x => x.typeDocument === 'Facture');
      this.bonsLivraison = d.filter(x => x.typeDocument === 'BonLivraison');
      
      // Si un fournisseur est spécifié, filtrer par fournisseur
      if (this.supplier) {
        this.factures = this.factures.filter(f => f.supplier === this.supplier);
      }
      
      // Recharger les relations pour mettre à jour la map
      this.loadRelations();
    });
  }

  loadRelations() {
    this.docs.relations().subscribe(rels => {
      this.relations = rels;
      const map: Record<number, string> = {};
      const invoiceToDelivery: Record<number, Document> = {};
      
      for (const r of rels) {
        map[r.invoiceId] = `→ BL #${r.deliveryId}`;
        map[r.deliveryId] = `→ Facture #${r.invoiceId}`;
        
        // Trouver le BL associé à cette facture
        const delivery = this.bonsLivraison.find(bl => bl.id === r.deliveryId);
        if (delivery) {
          invoiceToDelivery[r.invoiceId] = delivery;
        }
      }
      
      this.relationMap = map;
      this.invoiceToDeliveryMap = invoiceToDelivery;
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

  search() {
    if (!this.query) {
      this.load();
      return;
    }
    this.docs.search(this.query).subscribe(d => {
      this.documents = d;
      this.factures = d.filter(x => x.typeDocument === 'Facture');
      this.bonsLivraison = d.filter(x => x.typeDocument === 'BonLivraison');
    });
  }

  selectInvoice(invoice: Document) {
    this.selectedInvoice = invoice;
    // Si un BL est déjà sélectionné, proposer l'association
    if (this.selectedDelivery) {
      this.linkSelected();
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
    this.docs.compare(invoiceId, deliveryId).subscribe(res => {
      this.comparaisonResult = res;
      this.invoicePriceComparisonResult = null; // Clear invoice comparison
      this.snack.open('Comparaison effectuée', 'OK', { duration: 2000 });
    });
  }

  compareInvoices() {
    if (!this.selectedInvoice1 || !this.selectedInvoice2) {
      this.snack.open('Veuillez sélectionner deux factures', 'OK', { duration: 2000 });
      return;
    }
    this.docs.compareInvoices(this.selectedInvoice1.id, this.selectedInvoice2.id).subscribe({
      next: (res) => {
        this.invoicePriceComparisonResult = res;
        this.comparaisonResult = null; // Clear other comparison
        this.snack.open('Comparaison de prix effectuée', 'OK', { duration: 2000 });
      },
      error: (err) => {
        console.error('Erreur lors de la comparaison:', err);
        this.snack.open('Erreur lors de la comparaison', 'OK', { duration: 2000 });
      }
    });
  }

  compareAndStock(invoiceId: number, deliveryId: number) {
    this.docs.compareAndStock(invoiceId, deliveryId).subscribe({
      next: (r) => {
        if (r.success) {
          this.snack.open('Stock mis à jour (quantités livrées)', 'OK', { duration: 2500 });
        } else {
          this.snack.open('Différences détectées: stock non mis à jour', 'OK', { duration: 3000 });
        }
      },
      error: (e) => {
        console.error(e);
        this.snack.open('Erreur lors de l\'alimentation du stock', 'Fermer', { duration: 3000 });
      }
    });
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

  getAssociatedDelivery(invoiceId: number): Document | null {
    return this.invoiceToDeliveryMap[invoiceId] || null;
  }

  getRelationId(invoiceId: number, deliveryId: number): number | null {
    const relation = this.relations.find(r => r.invoiceId === invoiceId && r.deliveryId === deliveryId);
    return relation ? relation.id : null;
  }
}

