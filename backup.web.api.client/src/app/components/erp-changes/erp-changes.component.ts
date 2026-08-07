import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MaterialModule } from '../../material.module';
import { ErpChangeValueMode, ErpProductChange, ErpSyncLog } from '../../models/erp-product';
import { ErpProductService } from '../../services/erp-product.service';
import { Subject, Subscription, debounceTime, distinctUntilChanged, switchMap, takeWhile, timer } from 'rxjs';
import { AppI18nService } from '../../services/app-i18n.service';
import { TPipe } from '../../pipes/t.pipe';
import { PermissionService } from '../../services/permission.service';
import { Permissions } from '../../constants/permissions';
import { FormHelpComponent } from '../shared/form-help/form-help.component';
import { TableSortState } from '../../utils/table-sort';
import { SortableThComponent } from '../shared/sortable-th/sortable-th.component';
import { CompanyService } from '../../services/company.service';

@Component({
  selector: 'app-erp-changes',
  templateUrl: './erp-changes.component.html',
  styleUrls: ['./erp-changes.component.css'],
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, RouterModule, TPipe, FormHelpComponent, SortableThComponent]
})
export class ErpChangesComponent implements OnInit, OnDestroy {
  changes: ErpProductChange[] = [];
  syncLogs: ErpSyncLog[] = [];
  changeSort = new TableSortState('detectedAt', 'desc');
  syncLogSort = new TableSortState('startedAt', 'desc');
  total = 0;
  page = 1;
  pageSize = 50;
  loading = false;
  syncing = false;
  importing = false;
  cleaning = false;

  syncProgress: ErpSyncLog | null = null;
  private syncPollSub: Subscription | null = null;

  unreadOnly = true;
  changeType = '';
  valueMode: ErpChangeValueMode = 'both';
  search = '';
  selectedIds = new Set<number>();

  readonly changeTypes = [
    { value: '', labelKey: 'erpChanges.filter.allTypes' },
    { value: 'Created', labelKey: 'erpChanges.filter.created' },
    { value: 'Updated', labelKey: 'erpChanges.filter.updated' },
    { value: 'PriceChanged', labelKey: 'erpChanges.filter.price' },
    { value: 'StockChanged', labelKey: 'erpChanges.filter.stock' },
    { value: 'Deleted', labelKey: 'erpChanges.filter.deleted' }
  ];

  readonly valueModes: { value: ErpChangeValueMode; labelKey: string }[] = [
    { value: '', labelKey: 'erpChanges.filter.allValues' },
    { value: 'both', labelKey: 'erpChanges.filter.bothValues' },
    { value: 'cleared', labelKey: 'erpChanges.filter.cleared' },
    { value: 'added', labelKey: 'erpChanges.filter.added' }
  ];

  private searchInput$ = new Subject<string>();
  private searchSub: Subscription | null = null;

  constructor(
    private erpService: ErpProductService,
    private snack: MatSnackBar,
    private i18n: AppI18nService,
    public perm: PermissionService,
    private company: CompanyService,
    private router: Router
  ) {}

  readonly P = Permissions;

  ngOnInit(): void {
    if (!this.company.hasErpCatalogSync) {
      void this.router.navigate(['/erp-products']);
      return;
    }
    this.searchSub = this.searchInput$.pipe(
      debounceTime(300),
      distinctUntilChanged()
    ).subscribe(() => this.applyFilters());

    this.loadChanges();
    this.loadSyncLogs();
  }

  ngOnDestroy(): void {
    this.stopSyncPoll();
    this.searchSub?.unsubscribe();
    this.searchSub = null;
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

  loadChanges(): void {
    this.loading = true;
    this.erpService.getChanges({
      unreadOnly: this.unreadOnly ? true : undefined,
      changeType: this.changeType || undefined,
      valueMode: this.valueMode || undefined,
      q: this.search.trim() || undefined,
      page: this.page,
      pageSize: this.pageSize
    }).subscribe({
      next: (res) => {
        this.changes = res.items ?? [];
        this.total = res.total ?? 0;
        this.page = res.page ?? this.page;
        this.selectedIds.clear();
        this.loading = false;
      },
      error: (err) => {
        console.error(err);
        this.loading = false;
        this.snack.open(this.i18n.t('erpChanges.snack.loadError'), this.i18n.t('common.close'), { duration: 3500 });
      }
    });
  }

  onSearchInput(): void {
    this.searchInput$.next(this.search.trim());
  }

  loadSyncLogs(): void {
    this.erpService.getSyncLogs(1, 5).subscribe({
      next: (res) => {
        this.syncLogs = res.items ?? [];
      },
      error: () => {
        this.syncLogs = [];
      }
    });
  }

  applyFilters(): void {
    this.page = 1;
    this.loadChanges();
  }

  clearFilters(): void {
    this.unreadOnly = true;
    this.changeType = '';
    this.valueMode = 'both';
    this.search = '';
    this.page = 1;
    this.loadChanges();
  }

  toggleSelect(id: number): void {
    if (this.selectedIds.has(id)) {
      this.selectedIds.delete(id);
    } else {
      this.selectedIds.add(id);
    }
  }

  toggleSelectAll(): void {
    if (this.selectedIds.size === this.changes.length) {
      this.selectedIds.clear();
      return;
    }
    this.changes.forEach(c => this.selectedIds.add(c.id));
  }

  isSelected(id: number): boolean {
    return this.selectedIds.has(id);
  }

  get allSelected(): boolean {
    return this.changes.length > 0 && this.selectedIds.size === this.changes.length;
  }

  markSelectedRead(): void {
    const ids = Array.from(this.selectedIds);
    if (ids.length === 0) {
      this.snack.open(this.i18n.t('erpChanges.snack.selectAtLeastOne'), this.i18n.t('common.close'), { duration: 2500 });
      return;
    }
    this.erpService.markChangesRead(ids).subscribe({
      next: () => {
        this.snack.open(this.i18n.t('erpChanges.snack.markedRead', { count: ids.length }), this.i18n.t('common.ok'), { duration: 2500 });
        this.loadChanges();
      },
      error: () => this.snack.open('Impossible de marquer comme lu', this.i18n.t('common.close'), { duration: 3000 })
    });
  }

  deleteSelected(): void {
    const ids = Array.from(this.selectedIds);
    if (ids.length === 0) {
      this.snack.open(this.i18n.t('erpChanges.snack.selectAtLeastOne'), this.i18n.t('common.close'), { duration: 2500 });
      return;
    }
    this.erpService.deleteChanges(ids).subscribe({
      next: (res) => {
        this.snack.open(this.i18n.t('erpChanges.snack.deleted', { count: res.deleted }), this.i18n.t('common.ok'), { duration: 2500 });
        this.loadChanges();
      },
      error: () => this.snack.open('Impossible de supprimer la sélection', this.i18n.t('common.close'), { duration: 3000 })
    });
  }

  markAllVisibleRead(): void {
    const ids = this.changes.filter(c => !c.isRead).map(c => c.id);
    if (ids.length === 0) {
      this.snack.open('Aucun changement non lu sur cette page', this.i18n.t('common.close'), { duration: 2500 });
      return;
    }
    this.erpService.markChangesRead(ids).subscribe({
      next: () => {
        this.snack.open(this.i18n.t('erpChanges.snack.markedRead', { count: ids.length }), this.i18n.t('common.ok'), { duration: 2500 });
        this.loadChanges();
      },
      error: () => this.snack.open('Impossible de marquer comme lu', this.i18n.t('common.close'), { duration: 3000 })
    });
  }

  cleanupFormattingFalsePositives(): void {
    if (this.cleaning) return;
    this.cleaning = true;
    this.erpService.cleanupFormattingFalsePositives().subscribe({
      next: (res) => {
        this.cleaning = false;
        this.snack.open(
          res.deleted > 0
            ? `${res.deleted} faux changement(s) supprimé(s)`
            : 'Aucun faux changement de formatage trouvé',
          'OK',
          { duration: 4000 }
        );
        this.loadChanges();
      },
      error: () => {
        this.cleaning = false;
        this.snack.open('Impossible de nettoyer les faux changements', 'Fermer', { duration: 3000 });
      }
    });
  }

  triggerSyncAll(): void {
    if (this.syncing) return;
    this.syncing = true;
    this.syncProgress = null;
    this.snack.open(this.i18n.t('erpChanges.snack.enrichStarted'), undefined, { duration: 2500 });
    this.erpService.syncAll().subscribe({
      next: (log) => this.watchSyncJob(log),
      error: () => {
        this.syncing = false;
        this.syncProgress = null;
        this.snack.open(this.i18n.t('erpProducts.snack.syncFailed'), this.i18n.t('common.close'), { duration: 4000 });
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
        this.syncing = false;
        this.stopSyncPoll();
        this.snack.open('Impossible de suivre la progression du sync', 'Fermer', { duration: 4000 });
      }
    });
  }

  private onSyncFinished(log: ErpSyncLog): void {
    this.syncing = false;
    this.stopSyncPoll();
    this.syncProgress = log;
    this.snack.open(
      `Sync ${log.status}: +${log.newProducts} créés, ${log.updatedProducts} maj, ${log.failedProducts} échecs`,
      'OK',
      { duration: 6000 }
    );
    this.loadChanges();
    this.loadSyncLogs();
  }

  private stopSyncPoll(): void {
    this.syncPollSub?.unsubscribe();
    this.syncPollSub = null;
  }

  importExcel(syncAfter = false): void {
    if (this.importing) return;
    this.importing = true;
    this.snack.open(
      syncAfter ? this.i18n.t('erpChanges.snack.importExcelSync') : this.i18n.t('erpChanges.snack.importExcel'),
      undefined,
      { duration: 3000 }
    );
    this.erpService.importExcel(syncAfter).subscribe({
      next: (res) => {
        this.importing = false;
        const imp = res.import;
        const errCount = imp.errors?.length ?? 0;
        this.snack.open(
          `Excel: ${imp.created} créés, ${imp.updated} maj, ${imp.skipped} ignorés` +
            (errCount ? ` (${errCount} erreurs)` : ''),
          this.i18n.t('common.ok'),
          { duration: 6000 }
        );
        this.loadChanges();
        this.loadSyncLogs();
      },
      error: () => {
        this.importing = false;
        this.snack.open(this.i18n.t('erpChanges.snack.importFailed'), this.i18n.t('common.close'), { duration: 4000 });
      }
    });
  }

  prevPage(): void {
    if (this.page <= 1) return;
    this.page -= 1;
    this.loadChanges();
  }

  nextPage(): void {
    if (this.page * this.pageSize >= this.total) return;
    this.page += 1;
    this.loadChanges();
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.total / this.pageSize));
  }

  formatDate(value?: string | null): string {
    if (!value) return '—';
    const date = new Date(value);
    return date.toLocaleString(this.i18n.numberLocale(), {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  changeTypeLabel(type: string): string {
    const key = this.changeTypes.find(t => t.value === type)?.labelKey;
    return key ? this.i18n.t(key) : type;
  }

  changeTypeClass(type: string): string {
    switch (type) {
      case 'Created': return 'chip-created';
      case 'PriceChanged': return 'chip-price';
      case 'StockChanged': return 'chip-stock';
      case 'Deleted': return 'chip-deleted';
      default: return 'chip-updated';
    }
  }

  fieldLabel(field: string): string {
    const map: Record<string, string> = {
      '*': 'Produit',
      Name: 'Nom',
      Name2: 'Nom 2',
      Reference: 'Référence',
      Ean: 'EAN',
      Brand: 'Marque',
      UnitPrice: 'Prix vente TTC',
      PriceHT: 'Prix vente HT',
      CPrice: 'Prix d\'achat',
      RPrice: 'Prix détail',
      DiscountPrice: 'Prix remisé',
      StockQuantity: 'Stock',
      Comment: 'Commentaire',
      TypeName: 'Type',
      SubTypeName: 'Sous-type',
      MainTypeName: 'Catégorie',
      PromoActive: 'Promo active',
      PromoPrice: 'Prix promo',
      Archived: 'Archivé'
    };
    return map[field] ?? field;
  }

  productTitle(change: ErpProductChange): string {
    const p = change.product;
    if (!p) return `Produit #${change.erpProductId}`;
    return p.name || p.reference || p.erpProductId || `Produit #${change.erpProductId}`;
  }

  get sortedChanges(): ErpProductChange[] {
    void this.changeSort.version;
    return this.changeSort.sort(this.changes, {
      detectedAt: c => c.detectedAt ?? '',
      changeType: c => c.changeType ?? '',
      product: c => this.productTitle(c),
      fieldName: c => this.fieldLabel(c.fieldName),
      oldValue: c => c.oldValue ?? '',
      newValue: c => c.newValue ?? '',
      isRead: c => c.isRead ? 1 : 0
    });
  }

  get sortedSyncLogs(): ErpSyncLog[] {
    void this.syncLogSort.version;
    return this.syncLogSort.sort(this.syncLogs, {
      startedAt: l => l.startedAt ?? '',
      status: l => l.status ?? '',
      newProducts: l => l.newProducts ?? 0,
      updatedProducts: l => l.updatedProducts ?? 0,
      failedProducts: l => l.failedProducts ?? 0,
      totalChanges: l => l.totalChanges ?? 0
    });
  }

  unreadCount(): number {
    return this.changes.filter(c => !c.isRead).length;
  }
}
