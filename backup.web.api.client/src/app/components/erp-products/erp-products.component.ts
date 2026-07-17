import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MaterialModule } from '../../material.module';
import { ErpProduct } from '../../models/erp-product';
import { ErpProductService } from '../../services/erp-product.service';

@Component({
  selector: 'app-erp-products',
  templateUrl: './erp-products.component.html',
  styleUrls: ['./erp-products.component.css'],
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, RouterModule]
})
export class ErpProductsComponent implements OnInit {
  products: ErpProduct[] = [];
  selected: ErpProduct | null = null;
  total = 0;
  page = 1;
  pageSize = 50;
  loading = false;
  syncingId: number | null = null;

  searchQuery = '';
  brandFilter = '';
  sourceFilter = '';

  readonly sourceOptions = [
    { value: '', label: 'Toutes sources' },
    { value: 'Excel', label: 'Excel' },
    { value: 'Merged', label: 'Excel + ERP' },
    { value: 'Erp', label: 'ERP seul' }
  ];

  constructor(
    private erpService: ErpProductService,
    private snack: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.loadProducts();
  }

  loadProducts(): void {
    this.loading = true;
    this.erpService.getProducts({
      page: this.page,
      pageSize: this.pageSize,
      q: this.searchQuery.trim() || undefined,
      brand: this.brandFilter.trim() || undefined,
      dataSource: this.sourceFilter || undefined
    }).subscribe({
      next: (res) => {
        this.products = res.items ?? [];
        this.total = res.total ?? 0;
        this.page = res.page ?? this.page;
        this.loading = false;
        if (this.selected) {
          const refreshed = this.products.find(p => p.id === this.selected!.id);
          if (refreshed) this.selected = refreshed;
        }
      },
      error: (err) => {
        console.error(err);
        this.loading = false;
        this.snack.open('Erreur chargement produits', 'Fermer', { duration: 3500 });
      }
    });
  }

  applyFilters(): void {
    this.page = 1;
    this.loadProducts();
  }

  clearFilters(): void {
    this.searchQuery = '';
    this.brandFilter = '';
    this.sourceFilter = '';
    this.page = 1;
    this.loadProducts();
  }

  selectProduct(product: ErpProduct): void {
    this.selected = product;
  }

  closeDetail(): void {
    this.selected = null;
  }

  syncProduct(product: ErpProduct, event?: Event): void {
    event?.stopPropagation();
    if (this.syncingId != null) return;

    this.syncingId = product.id;
    this.erpService.syncProduct(product).subscribe({
      next: (updated) => {
        this.syncingId = null;
        const idx = this.products.findIndex(p => p.id === product.id);
        if (idx >= 0) this.products[idx] = { ...this.products[idx], ...updated };
        if (this.selected?.id === product.id) this.selected = { ...this.selected, ...updated };
        this.snack.open(
          `Sync OK — ${updated.name || updated.reference || updated.erpProductId}`,
          'OK',
          { duration: 3000 }
        );
      },
      error: (err) => {
        this.syncingId = null;
        const detail = err?.error?.detail || err?.error?.message || err?.message;
        this.snack.open(
          detail ? `Échec sync: ${detail}` : 'Échec sync ERP pour ce produit',
          'Fermer',
          { duration: 8000 }
        );
      }
    });
  }

  prevPage(): void {
    if (this.page <= 1) return;
    this.page -= 1;
    this.loadProducts();
  }

  nextPage(): void {
    if (this.page * this.pageSize >= this.total) return;
    this.page += 1;
    this.loadProducts();
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.total / this.pageSize));
  }

  formatDate(value?: string | null): string {
    if (!value) return '—';
    return new Date(value).toLocaleString('fr-FR', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  formatPrice(value?: number | null): string {
    if (value == null) return '—';
    return value.toLocaleString('fr-BE', { minimumFractionDigits: 2, maximumFractionDigits: 4 });
  }

  sourceClass(source?: string | null): string {
    switch (source) {
      case 'Excel': return 'src-excel';
      case 'Merged': return 'src-merged';
      case 'Erp': return 'src-erp';
      default: return 'src-unknown';
    }
  }

  isSynced(product: ErpProduct): boolean {
    return !!product.lastSyncAt;
  }
}
