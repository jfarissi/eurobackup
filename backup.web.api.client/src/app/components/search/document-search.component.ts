import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { Subscription, finalize } from 'rxjs';
import { DocumentService } from '../../services/document.service';
import { MaterialModule } from '../../material.module';
import { Document } from '../../models/document';
import { AppI18nService } from '../../services/app-i18n.service';
import { TPipe } from '../../pipes/t.pipe';

@Component({
  selector: 'app-document-search',
  templateUrl: './document-search.component.html',
  styleUrls: ['./document-search.component.css'],
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, RouterModule, TPipe]
})
export class DocumentSearchComponent implements OnInit, OnDestroy {
  query = '';
  loading = false;
  hasSearched = false;
  errorMessage: string | null = null;
  results: Document[] = [];
  factures: Document[] = [];
  bonsLivraison: Document[] = [];
  autresDocuments: Document[] = [];
  private routeSub: Subscription | null = null;

  constructor(
    private docs: DocumentService,
    private router: Router,
    private route: ActivatedRoute,
    private i18n: AppI18nService
  ) {}

  ngOnInit(): void {
    this.routeSub = this.route.queryParamMap.subscribe(params => {
      const q = (params.get('q') || '').trim();
      const view = (params.get('view') || '').trim().toLowerCase();
      this.query = q;
      if (q) {
        this.search();
      } else if (view === 'all' || view === 'recent') {
        this.loadAllDocuments();
      } else {
        this.clearResults();
      }
    });
  }

  ngOnDestroy(): void {
    this.routeSub?.unsubscribe();
  }

  search(): void {
    const term = this.query.trim();
    if (!term) {
      this.clearResults();
      void this.router.navigate(['/recherche'], { queryParams: {} });
      return;
    }

    // Garde l’URL synchronisée avec la barre du topbar
    const current = this.route.snapshot.queryParamMap.get('q') || '';
    if (current !== term) {
      void this.router.navigate(['/recherche'], { queryParams: { q: term } });
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
        this.errorMessage = this.i18n.t('search.error.load');
        this.results = [];
        this.factures = [];
        this.bonsLivraison = [];
        this.autresDocuments = [];
      }
    });
  }

  /** Liste tous les documents (depuis le dashboard « Voir tout »). */
  private loadAllDocuments(): void {
    this.loading = true;
    this.hasSearched = true;
    this.errorMessage = null;
    this.docs.list().pipe(
      finalize(() => { this.loading = false; })
    ).subscribe({
      next: (docs) => {
        const sorted = [...(docs || [])].sort((a, b) => {
          const ta = a.dateAdded ? new Date(a.dateAdded).getTime() : 0;
          const tb = b.dateAdded ? new Date(b.dateAdded).getTime() : 0;
          return tb - ta;
        });
        this.applyResults(sorted);
      },
      error: () => {
        this.errorMessage = this.i18n.t('search.error.load');
        this.results = [];
        this.factures = [];
        this.bonsLivraison = [];
        this.autresDocuments = [];
      }
    });
  }

  clear(): void {
    this.query = '';
    void this.router.navigate(['/recherche'], { queryParams: {} });
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
