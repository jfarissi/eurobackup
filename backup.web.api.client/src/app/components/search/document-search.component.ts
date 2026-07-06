import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { finalize } from 'rxjs';
import { DocumentService } from '../../services/document.service';
import { MaterialModule } from '../../material.module';
import { Document } from '../../models/document';

@Component({
  selector: 'app-document-search',
  templateUrl: './document-search.component.html',
  styleUrls: ['./document-search.component.css'],
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, RouterModule]
})
export class DocumentSearchComponent {
  query = '';
  loading = false;
  hasSearched = false;
  errorMessage: string | null = null;
  results: Document[] = [];
  factures: Document[] = [];
  bonsLivraison: Document[] = [];
  autresDocuments: Document[] = [];

  constructor(
    private docs: DocumentService,
    private router: Router
  ) {}

  search(): void {
    const term = this.query.trim();
    if (!term) {
      this.clearResults();
      return;
    }

    this.loading = true;
    this.hasSearched = true;
    this.errorMessage = null;

    this.docs.search(term).pipe(
      finalize(() => { this.loading = false; })
    ).subscribe({
      next: (docs) => this.applyResults(docs),
      error: () => {
        this.errorMessage = 'Impossible de charger les documents. Vérifiez que l\'API est démarrée.';
        this.results = [];
        this.factures = [];
        this.bonsLivraison = [];
        this.autresDocuments = [];
      }
    });
  }

  clear(): void {
    this.query = '';
    this.clearResults();
  }

  private clearResults(): void {
    this.hasSearched = false;
    this.errorMessage = null;
    this.results = [];
    this.factures = [];
    this.bonsLivraison = [];
    this.autresDocuments = [];
  }

  private applyResults(docs: Document[]): void {
    this.results = docs;
    this.factures = docs.filter(d => this.isFactureDoc(d));
    this.bonsLivraison = docs.filter(d => this.isBonLivraisonDoc(d));
    this.autresDocuments = docs.filter(d => !this.isBonLivraisonDoc(d) && !this.isFactureDoc(d));
  }

  isFacture(doc: Document): boolean {
    return this.isFactureDoc(doc);
  }

  isBl(doc: Document): boolean {
    return this.isBonLivraisonDoc(doc);
  }

  isOther(doc: Document): boolean {
    return !this.isFactureDoc(doc) && !this.isBonLivraisonDoc(doc);
  }

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

  openInCompare(doc: Document): void {
    if (this.isBonLivraisonDoc(doc)) {
      this.router.navigate(['/compare'], {
        queryParams: {
          blId: doc.id,
          blNumber: doc.numero ?? '',
          supplier: doc.supplier ?? ''
        }
      });
      return;
    }
    this.router.navigate(['/compare'], {
      queryParams: { invoiceId: doc.id, supplier: doc.supplier ?? '' }
    });
  }

  download(id: number): void {
    this.docs.download(id).subscribe(blob => {
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'document.pdf';
      a.click();
      window.URL.revokeObjectURL(url);
    });
  }
}
