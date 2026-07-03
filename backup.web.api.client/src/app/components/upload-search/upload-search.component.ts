import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DocumentService } from '../../services/document.service';
import { CdkDragDrop, moveItemInArray, transferArrayItem } from '@angular/cdk/drag-drop';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MaterialModule } from '../../material.module';
import { Document } from '../../models/document';
import { DocumentRelation } from '../../models/relation';
import { ComparisonResult } from '../../models/comparison';

@Component({
  selector: 'app-upload-search',
  templateUrl: './upload-search.component.html',
  styleUrls: ['./upload-search.component.css'],
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule]
})
export class UploadSearchComponent implements OnInit {
  file: File | null = null;
  typeDocument = 'Facture';
  numero = '';
  client = '';
  supplier = '';
  dateDocument = '';
  query = '';
  documents: Document[] = [];
  factures: Document[] = [];
  bonsLivraison: Document[] = [];
  relationMap: Record<number, string> = {};
  relations: DocumentRelation[] = [];
  dragItem: Document | null = null;
  comparaisonResult: ComparisonResult | null = null;
  selectedInvoice: Document | null = null;
  selectedDelivery: Document | null = null;
  pairItems: Document[] = [];
  factureDropIds: string[] = [];
  blDropIds: string[] = [];

  constructor(private docs: DocumentService, private snack: MatSnackBar) {}

  ngOnInit(): void {
    this.load();
    this.loadRelations();
  }

  onFileChange(event: any) {
    this.file = event.target.files[0];
    if (this.file) {
      this.docs.inspect(this.file).subscribe({
        next: (r) => {
          if (r?.typeDocument) this.typeDocument = r.typeDocument;
          if (r?.numero) this.numero = r.numero;
          if (r?.client) this.client = r.client;
          if (r?.supplier) this.supplier = r.supplier;
          if (r?.dateDocument) this.dateDocument = (r.dateDocument.length > 10 ? r.dateDocument.substring(0, 10) : r.dateDocument);
        },
        error: _ => { /* silent */ }
      });
    }
  }

  upload() {
    if (!this.file) return;
    this.docs.upload(this.file, this.typeDocument, this.numero, this.client, this.dateDocument, this.supplier)
      .subscribe(() => {
        this.file = null;
        this.load();
      });
  }

  load() {
    this.docs.list().subscribe(d => {
      this.documents = d;
      this.factures = d.filter(x => x.typeDocument === 'Facture');
      this.bonsLivraison = d.filter(x => x.typeDocument === 'BonLivraison');
      this.factureDropIds = this.factures.map(f => 'fact-' + f.id);
      this.blDropIds = this.bonsLivraison.map(b => 'bl-' + b.id);
    });
  }

  search() {
    if (!this.query) { this.load(); return; }
    this.docs.search(this.query).subscribe(d => this.documents = d);
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

  loadRelations() {
    this.docs.relations().subscribe(rels => {
      this.relations = rels;
      const map: Record<number, string> = {};
      for (const r of rels) {
        map[r.invoiceId] = `-> BL #${r.deliveryId}`;
        map[r.deliveryId] = `-> Facture #${r.invoiceId}`;
      }
      this.relationMap = map;
    });
  }

  onDragStart(item: any) {
    this.dragItem = item;
    console.log('Drag start', item);
  }

  onDropList(event: CdkDragDrop<any[]>, targetType: 'Facture' | 'BonLivraison') {
    if (event.previousContainer === event.container) {
      moveItemInArray(event.container.data, event.previousIndex, event.currentIndex);
    } else {
      transferArrayItem(event.previousContainer.data, event.container.data, event.previousIndex, event.currentIndex);
      const dropped = event.container.data[event.currentIndex] as any;
      const draggedItem = this.dragItem || (event.item?.data as any);
      if (!dropped || !draggedItem) { this.dragItem = null; return; }
      if (targetType === 'BonLivraison' && dropped.typeDocument === 'BonLivraison' && draggedItem.typeDocument === 'Facture') {
        this.docs.link(draggedItem.id, dropped.id).subscribe(() => this.loadRelations());
      }
      this.dragItem = null;
    }
  }

  onCardDrop(event: CdkDragDrop<any>, targetType: 'Facture' | 'BonLivraison') {
    const droppedCard = event.container.data as any;
    const draggedCard = event.item?.data as any;
    console.log('Drop', { droppedCard, draggedCard, targetType });
    if (!droppedCard || !draggedCard) return;
    if (draggedCard.typeDocument === droppedCard.typeDocument) {
      this.snack.open('Déposez sur une carte du type opposé (Facture ↔ BL)', 'OK', { duration: 2500 });
      return;
    }
    // If a Facture is dropped onto a BL card → link(FACTURE, BL)
    if (draggedCard.typeDocument === 'Facture' && droppedCard.typeDocument === 'BonLivraison') {
      this.docs.link(draggedCard.id, droppedCard.id).subscribe({
        next: () => { this.snack.open(`Relation Facture #${draggedCard.id} → BL #${droppedCard.id} créée`, 'OK', { duration: 2500 }); this.loadRelations(); },
        error: (e) => { console.error(e); this.snack.open('Erreur création relation', 'Fermer', { duration: 3000 }); }
      });
      return;
    }
    // If a BL is dropped onto a Facture card → link(FACTURE, BL)
    if (draggedCard.typeDocument === 'BonLivraison' && droppedCard.typeDocument === 'Facture') {
      this.docs.link(droppedCard.id, draggedCard.id).subscribe({
        next: () => { this.snack.open(`Relation Facture #${droppedCard.id} → BL #${draggedCard.id} créée`, 'OK', { duration: 2500 }); this.loadRelations(); },
        error: (e) => { console.error(e); this.snack.open('Erreur création relation', 'Fermer', { duration: 3000 }); }
      });
      return;
    }
  }

  onPairDrop(event: CdkDragDrop<any>) {
    const draggedCard = event.item?.data as any;
    console.log('PairDrop', draggedCard);
    if (!draggedCard) return;
    if (draggedCard.typeDocument === 'Facture') this.selectedInvoice = draggedCard;
    if (draggedCard.typeDocument === 'BonLivraison') this.selectedDelivery = draggedCard;
    this.snack.open('Sélection mise à jour', 'OK', { duration: 1500 });
  }

  clearInvoice() { this.selectedInvoice = null; }
  clearDelivery() { this.selectedDelivery = null; }
  clearPair() { this.selectedInvoice = null; this.selectedDelivery = null; }

  linkSelected() {
    if (!this.selectedInvoice || !this.selectedDelivery) return;
    this.docs.link(this.selectedInvoice.id, this.selectedDelivery.id).subscribe({
      next: () => { this.snack.open('Relation créée', 'OK', { duration: 2000 }); this.loadRelations(); this.clearPair(); },
      error: (e) => { console.error(e); this.snack.open('Erreur création relation', 'Fermer', { duration: 3000 }); }
    });
  }

  unlink(relationId: number) {
    if (!confirm('Confirmer la dissociation ?')) return;
    this.docs.unlink(relationId).subscribe(() => this.loadRelations());
  }

  compare(invoiceId: number, deliveryId: number) {
    this.docs.compare(invoiceId, deliveryId).subscribe(res => {
      // quick view in console; can render under the table
      console.log('Comparison', res);
      this.comparaisonResult = res;
    });
  }

  compareAndStock(invoiceId: number, deliveryId: number) {
    this.docs.compareAndStock(invoiceId, deliveryId, false).subscribe({
      next: (r) => {
        if (r.success) {
          this.snack.open('Stock mis à jour (quantités livrées)', 'OK', { duration: 2500 });
        } else {
          this.snack.open('Différences détectées: stock non mis à jour', 'OK', { duration: 3000 });
        }
      },
      error: (e) => {
        console.error(e);
        this.snack.open('Erreur lors de l’alimentation du stock', 'Fermer', { duration: 3000 });
      }
    });
  }

  onReparse(documentId: number) {
    if (!documentId) return;
    this.docs.reparseLines(documentId, false).subscribe({
      next: r => {
        if (r.success) {
          this.snack.open('Document re-parsé avec succès', 'OK', { duration: 2000 });
          this.load();
        } else {
          this.snack.open('Erreur lors du re-parsing', 'Fermer', { duration: 3000 });
        }
      },
      error: _ => this.snack.open('Erreur reparse', 'Fermer', { duration: 3000 })
    });
  }

}


