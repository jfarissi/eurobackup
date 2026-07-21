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
  syncMode: 'catalog' | 'enrich' | null = null;
  syncFilterLabel = '';
  private syncPollSub: Subscription | null = null;

  searchQuery = '';
  brandFilter = '';
  sourceFilter = '';
  mainTypeId = '';
  typeId = '';
  subTypeId = '';

  brands: ErpBrand[] = [];
  mainTypes: ErpCategory[] = [];
  types: ErpCategory[] = [];
  subTypes: ErpCategory[] = [];

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
    this.loadFilterOptions();
    this.loadProducts();
  }

  ngOnDestroy(): void {
    this.stopSyncPoll();
  }

  get hasSyncFilter(): boolean {
    return !!(
      this.brandFilter.trim()
      || this.mainTypeId
      || this.typeId
      || this.subTypeId
    );
  }

  get syncProgressTitle(): string {
    const mode = this.parseSyncDetails(this.syncProgress ?? ({} as ErpSyncLog)).mode;
    if (mode === 'FullCatalog') return 'Sync catalogue ERP complet';
    if (this.syncMode === 'catalog') return 'Sync produits filtrés';
    return 'Enrichissement ERP (produits locaux)';
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

    const details = this.parseSyncDetails(log);
    if (details.phase === 'collecting' || details.phase === 'starting' || this.syncProgressIndeterminate) {
      if (details.mode === 'FullCatalog') {
        return 'Collecte des IDs produits depuis l\'ERP… (peut prendre plusieurs minutes)';
      }
      return this.syncMode === 'catalog'
        ? 'Préparation de la sync filtrée…'
        : 'Préparation de l\'enrichissement…';
    }

    const processed = log.processedProducts ?? 0;
    const scope = details.mode === 'FullCatalog'
      ? 'produits ERP'
      : 'produits synchronisés';
    return `${processed} / ${log.totalProducts} ${scope}`
      + ` · +${log.newProducts} créés · ${log.updatedProducts} maj · ${log.failedProducts} échecs`;
  }

  private parseSyncDetails(log: ErpSyncLog): { mode?: string; phase?: string } {
    if (!log.details) return {};
    try {
      return JSON.parse(log.details) as { mode?: string; phase?: string };
    } catch {
      return {};
    }
  }

  private buildSyncFilterLabel(): string {
    const parts: string[] = [];
    if (this.brandFilter.trim()) parts.push(this.brandFilter.trim());

    const main = this.mainTypes.find(c => c.erpExternalId === this.mainTypeId);
    if (main) parts.push(this.categoryLabel(main));

    const type = this.types.find(c => c.erpExternalId === this.typeId);
    if (type) parts.push(this.categoryLabel(type));

    const sub = this.subTypes.find(c => c.erpExternalId === this.subTypeId);
    if (sub) parts.push(this.categoryLabel(sub));

    return parts.join(' / ');
  }

  private startSyncTracking(mode: 'catalog' | 'enrich'): void {
    this.syncMode = mode;
    this.syncFilterLabel = mode === 'catalog' ? this.buildSyncFilterLabel() : '';
    this.syncingAll = true;
    this.syncProgress = null;
  }

  private resetSyncTracking(): void {
    this.syncingAll = false;
    this.syncMode = null;
    this.syncFilterLabel = '';
    this.syncProgress = null;
  }

  private currentBrandFilter(): string | undefined {
    return this.brandFilter.trim() || undefined;
  }

  private currentCategoryFilter(): { mainTypeId?: string; typeId?: string; subTypeId?: string } {
    return {
      subTypeId: this.subTypeId || undefined,
      typeId: (!this.subTypeId && this.typeId) || undefined,
      mainTypeId: (!this.subTypeId && !this.typeId && this.mainTypeId) || undefined
    };
  }

  loadFilterOptions(): void {
    this.loadBrands();
    this.loadMainTypes();
  }

  loadBrands(): void {
    this.erpService.getBrands(this.currentCategoryFilter()).subscribe({
      next: (brands) => {
        this.brands = brands ?? [];
        if (this.brandFilter && !this.brands.some(b => b.name === this.brandFilter)) {
          this.brandFilter = '';
        }
      }
    });
  }

  loadMainTypes(): void {
    this.erpService.getCategories({
      level: 'MainType',
      brand: this.currentBrandFilter()
    }).subscribe({
      next: (items) => {
        this.mainTypes = items ?? [];
        if (this.mainTypeId && !this.mainTypes.some(c => c.erpExternalId === this.mainTypeId)) {
          this.mainTypeId = '';
          this.typeId = '';
          this.subTypeId = '';
          this.types = [];
          this.subTypes = [];
        }
      }
    });
  }

  onBrandFilterChange(): void {
    this.mainTypeId = '';
    this.typeId = '';
    this.subTypeId = '';
    this.types = [];
    this.subTypes = [];
    this.loadMainTypes();
    this.page = 1;
    this.loadProducts();
  }

  onMainTypeChange(): void {
    this.typeId = '';
    this.subTypeId = '';
    this.types = [];
    this.subTypes = [];

    const mainType = this.mainTypes.find(c => c.erpExternalId === this.mainTypeId);
    if (mainType) {
      this.erpService.getCategories({
        level: 'Type',
        parentId: mainType.id,
        brand: this.currentBrandFilter()
      }).subscribe({
        next: (items) => { this.types = items ?? []; }
      });
    }

    this.loadBrands();
    this.page = 1;
    this.loadProducts();
  }

  onTypeChange(): void {
    this.subTypeId = '';
    this.subTypes = [];

    const type = this.types.find(c => c.erpExternalId === this.typeId);
    if (type) {
      this.erpService.getCategories({
        level: 'SubType',
        parentId: type.id,
        brand: this.currentBrandFilter()
      }).subscribe({
        next: (items) => { this.subTypes = items ?? []; }
      });
    }

    this.loadBrands();
    this.page = 1;
    this.loadProducts();
  }

  onSubTypeChange(): void {
    this.loadBrands();
    this.page = 1;
    this.loadProducts();
  }

  categoryLabel(category: ErpCategory): string {
    return category.nameNl || category.nameFr || category.nameEn || category.erpExternalId;
  }

  loadProducts(): void {
    this.loading = true;
    this.erpService.getProducts({
      page: this.page,
      pageSize: this.pageSize,
      q: this.searchQuery.trim() || undefined,
      brand: this.currentBrandFilter(),
      dataSource: this.sourceFilter || undefined,
      subTypeId: this.subTypeId || undefined,
      typeId: (!this.subTypeId && this.typeId) || undefined,
      mainTypeId: (!this.subTypeId && !this.typeId && this.mainTypeId) || undefined,
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
    this.mainTypeId = '';
    this.typeId = '';
    this.subTypeId = '';
    this.types = [];
    this.subTypes = [];
    this.page = 1;
    this.loadFilterOptions();
    this.loadProducts();
  }

  triggerCatalogSync(): void {
    if (!this.hasSyncFilter || this.syncingAll || this.syncingId != null) return;

    this.startSyncTracking('catalog');
    this.snack.open(`Sync de ${this.total} produit(s) filtré(s)…`, undefined, { duration: 2500 });

    const category = this.currentCategoryFilter();
    this.erpService.syncCatalog({
      brand: this.currentBrandFilter(),
      mainTypeId: category.mainTypeId,
      typeId: category.typeId,
      subTypeId: category.subTypeId
    }, true).subscribe({
      next: (log) => this.watchSyncJob(log),
      error: (err) => {
        this.resetSyncTracking();
        const detail = err?.error?.detail || err?.error?.message || err?.message;
        this.snack.open(
          detail ? `Échec sync: ${detail}` : 'Échec du démarrage de la sync',
          'Fermer',
          { duration: 8000 }
        );
      }
    });
  }

  cancelSync(): void {
    this.erpService.cancelRunningSync().subscribe({
      next: () => {
        this.resetSyncTracking();
        this.snack.open('Sync annulée', 'OK', { duration: 3000 });
      },
      error: () => {
        this.resetSyncTracking();
        this.snack.open('Sync arrêtée (ou déjà terminée)', 'OK', { duration: 3000 });
      }
    });
  }

  triggerSyncAll(): void {
    if (this.syncingAll || this.syncingId != null) return;
    this.startSyncTracking('enrich');
    this.snack.open('Enrichissement ERP démarré…', undefined, { duration: 2500 });
    this.erpService.syncAll().subscribe({
      next: (log) => this.watchSyncJob(log),
      error: (err) => {
        this.resetSyncTracking();
        const detail = err?.error?.detail || err?.error?.message || err?.message;
        this.snack.open(
          detail ? `Échec sync: ${detail}` : 'Échec du démarrage de la synchronisation ERP',
          'Fermer',
          { duration: 8000 }
        );
      }
    });
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

  private watchSyncJob(log: ErpSyncLog): void {
    const details = this.parseSyncDetails(log);
    // Ancien job fantôme LocalEnrich massif (ex. 15k figés) — pas FullCatalog.
    const looksLikeStaleMassLocal =
      details.mode !== 'FullCatalog'
      && (log.processedProducts ?? 0) > 0
      && log.status === 'Running'
      && (log.totalProducts > this.total * 10 && this.total > 0);

    if (details.mode === 'CatalogFilter' || looksLikeStaleMassLocal) {
      this.snack.open(
        'Ancienne sync ERP détectée (job fantôme). Annulation… Relancez la sync.',
        'Fermer',
        { duration: 10000 }
      );
      this.erpService.cancelRunningSync().subscribe({
        next: () => this.resetSyncTracking(),
        error: () => this.resetSyncTracking()
      });
      return;
    }

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
        this.resetSyncTracking();
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
    this.loadFilterOptions();
    this.loadProducts();
    this.syncMode = null;
    this.syncFilterLabel = '';
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
