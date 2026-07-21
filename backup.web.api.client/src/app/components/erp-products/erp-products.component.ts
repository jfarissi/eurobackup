import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MaterialModule } from '../../material.module';
import { ErpBrand, ErpCategory, ErpProduct, ErpSyncLog } from '../../models/erp-product';
import { ErpProductService } from '../../services/erp-product.service';
import { Subscription, switchMap, takeWhile, timer } from 'rxjs';

@Component({
  selector: 'app-erp-products',
  templateUrl: './erp-products.component.html',
  styleUrls: ['./erp-products.component.css'],
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, RouterModule]
})
export class ErpProductsComponent implements OnInit, OnDestroy {
  products: ErpProduct[] = [];
  selected: ErpProduct | null = null;
  total = 0;
  page = 1;
  pageSize = 50;
  loading = false;
  syncingId: number | null = null;
  syncingAll = false;
  syncProgress: ErpSyncLog | null = null;
  private syncPollSub: Subscription | null = null;

  searchQuery = '';
  brandFilter = '';
  sourceFilter = '';

  brands: ErpBrand[] = [];
  mainTypes: ErpCategory[] = [];
  types: ErpCategory[] = [];
  subTypes: ErpCategory[] = [];
  catalogBrand = '';
  catalogMainTypeId = '';
  catalogTypeId = '';
  catalogSubTypeId = '';
  loadingCatalogOptions = false;

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
    this.loadCatalogOptions();
  }

  get hasCatalogFilter(): boolean {
    return !!(
      this.catalogBrand.trim()
      || this.catalogMainTypeId
      || this.catalogTypeId
      || this.catalogSubTypeId
    );
  }

  loadCatalogOptions(): void {
    this.loadingCatalogOptions = true;
    this.erpService.getBrands().subscribe({
      next: (brands) => {
        this.brands = brands ?? [];
        this.loadingCatalogOptions = false;
      },
      error: () => {
        this.loadingCatalogOptions = false;
      }
    });

    this.erpService.getCategories({ level: 'MainType' }).subscribe({
      next: (items) => {
        this.mainTypes = items ?? [];
      }
    });
  }

  onCatalogMainTypeChange(): void {
    this.catalogTypeId = '';
    this.catalogSubTypeId = '';
    this.types = [];
    this.subTypes = [];

    const mainType = this.mainTypes.find(c => c.erpExternalId === this.catalogMainTypeId);
    if (!mainType) return;

    this.erpService.getCategories({ level: 'Type', parentId: mainType.id }).subscribe({
      next: (items) => {
        this.types = items ?? [];
      }
    });
  }

  onCatalogTypeChange(): void {
    this.catalogSubTypeId = '';
    this.subTypes = [];

    const type = this.types.find(c => c.erpExternalId === this.catalogTypeId);
    if (!type) return;

    this.erpService.getCategories({ level: 'SubType', parentId: type.id }).subscribe({
      next: (items) => {
        this.subTypes = items ?? [];
      }
    });
  }

  clearCatalogFilters(): void {
    this.catalogBrand = '';
    this.catalogMainTypeId = '';
    this.catalogTypeId = '';
    this.catalogSubTypeId = '';
    this.types = [];
    this.subTypes = [];
  }

  categoryLabel(category: ErpCategory): string {
    return category.nameNl || category.nameFr || category.nameEn || category.erpExternalId;
  }

  triggerCatalogSync(): void {
    if (!this.hasCatalogFilter || this.syncingAll || this.syncingId != null) return;

    this.syncingAll = true;
    this.syncProgress = null;
    this.snack.open('Sync catalogue démarrée…', undefined, { duration: 2500 });

    this.erpService.syncCatalog({
      brand: this.catalogBrand.trim() || undefined,
      mainTypeId: this.catalogMainTypeId || undefined,
      typeId: this.catalogTypeId || undefined,
      subTypeId: this.catalogSubTypeId || undefined
    }).subscribe({
      next: (log) => this.watchSyncJob(log),
      error: (err) => {
        this.syncingAll = false;
        this.syncProgress = null;
        const detail = err?.error?.detail || err?.error?.message || err?.message;
        this.snack.open(
          detail ? `Échec sync catalogue: ${detail}` : 'Échec du démarrage de la sync catalogue',
          'Fermer',
          { duration: 8000 }
        );
      }
    });
  }

  ngOnDestroy(): void {
    this.stopSyncPoll();
  }

  get syncProgressPercent(): number {
    const log = this.syncProgress;
    if (!log || !log.totalProducts || log.totalProducts <= 0) return 0;
    const processed = log.processedProducts ?? 0;
    return Math.min(100, Math.round((processed / log.totalProducts) * 100));
  }

  get syncProgressIndeterminate(): boolean {
    return !!this.syncProgress
      && this.syncProgress.status === 'Running'
      && (!this.syncProgress.totalProducts || this.syncProgress.totalProducts <= 0);
  }

  get syncProgressLabel(): string {
    const log = this.syncProgress;
    if (!log) return '';
    if (this.syncProgressIndeterminate) {
      return 'Collecte du catalogue ERP…';
    }
    const processed = log.processedProducts ?? 0;
    return `${processed} / ${log.totalProducts} produits`
      + ` · +${log.newProducts} créés · ${log.updatedProducts} maj · ${log.failedProducts} échecs`;
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

  triggerSyncAll(): void {
    if (this.syncingAll || this.syncingId != null) return;
    this.syncingAll = true;
    this.syncProgress = null;
    this.snack.open('Enrichissement ERP démarré…', undefined, { duration: 2500 });
    this.erpService.syncAll().subscribe({
      next: (log) => this.watchSyncJob(log),
      error: (err) => {
        this.syncingAll = false;
        this.syncProgress = null;
        const detail = err?.error?.detail || err?.error?.message || err?.message;
        this.snack.open(
          detail ? `Échec sync: ${detail}` : 'Échec du démarrage de la synchronisation ERP',
          'Fermer',
          { duration: 8000 }
        );
      }
    });
  }

  private watchSyncJob(log: ErpSyncLog): void {
    this.syncProgress = log;
    this.stopSyncPoll();

    if (log.status !== 'Running') {
      this.onSyncFinished(log);
      return;
    }

    this.syncPollSub = timer(0, 1500).pipe(
      switchMap(() => this.erpService.getSyncLog(log.jobId)),
      takeWhile((current) => current.status === 'Running', true)
    ).subscribe({
      next: (current) => {
        this.syncProgress = current;
        if (current.status !== 'Running') {
          this.onSyncFinished(current);
        }
      },
      error: () => {
        this.syncingAll = false;
        this.stopSyncPoll();
        this.snack.open('Impossible de suivre la progression du sync', 'Fermer', { duration: 4000 });
      }
    });
  }

  private onSyncFinished(log: ErpSyncLog): void {
    this.syncingAll = false;
    this.stopSyncPoll();
    this.syncProgress = log;
    this.snack.open(
      `Sync ${log.status}: +${log.newProducts} créés, ${log.updatedProducts} maj, ${log.failedProducts} échecs`,
      'OK',
      { duration: 6000 }
    );
    this.loadProducts();
    this.loadCatalogOptions();
  }

  private stopSyncPoll(): void {
    this.syncPollSub?.unsubscribe();
    this.syncPollSub = null;
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
