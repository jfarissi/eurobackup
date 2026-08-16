import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { StockService } from '../../services/stock.service';
import { BusinessService } from '../../services/business.service';
import { StockForecastResult, StockItem } from '../../models/stock-item';
import { StockMovement } from '../../models/business';
import { MaterialModule } from '../../material.module';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AppI18nService } from '../../services/app-i18n.service';
import { TPipe } from '../../pipes/t.pipe';
import { PermissionService } from '../../services/permission.service';
import { Permissions } from '../../constants/permissions';
import { EmailService } from '../../services/email.service';
import { FormHelpComponent } from '../shared/form-help/form-help.component';
import { TableSortState } from '../../utils/table-sort';
import { SortableThComponent } from '../shared/sortable-th/sortable-th.component';

@Component({
  selector: 'app-stock',
  templateUrl: './stock.component.html',
  styleUrls: ['./stock.component.css'],
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, RouterModule, TPipe, FormHelpComponent, SortableThComponent]
})
export class StockComponent implements OnInit {
  selectedTab = 0;
  stockItems: StockItem[] = [];
  filteredItems: StockItem[] = [];
  stockBySupplier: { supplier: string; items: StockItem[] }[] = [];
  movements: StockMovement[] = [];
  searchQuery = '';
  movementFilter = '';
  expandedSuppliers = new Set<string>();
  stockSort = new TableSortState('productKey', 'asc');
  movementSort = new TableSortState('createdAt', 'desc');
  forecast: StockForecastResult | null = null;
  forecastError = false;
  showAllForecast = false;

  showAdjustModal = false;
  adjustError = '';
  runningStockAlerts = false;
  readonly P = Permissions;
  newMovement: StockMovement = this.createEmptyMovement();

  constructor(
    private stockService: StockService,
    private businessService: BusinessService,
    private snack: MatSnackBar,
    private i18n: AppI18nService,
    public perm: PermissionService,
    private emailService: EmailService,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    this.route.queryParamMap.subscribe(q => {
      if ((q.get('tab') || '').toLowerCase() === 'forecast') {
        this.selectedTab = 2;
      }
    });
    this.loadStock();
    this.loadMovements();
    this.loadForecast();
  }

  loadStock(): void {
    this.stockService.getAll(this.searchQuery || undefined).subscribe({
      next: (items) => {
        this.stockItems = items;
        this.filteredItems = items;
        this.groupBySupplier();
      },
      error: (err) => {
        console.error('Erreur lors du chargement du stock:', err);
        this.snack.open(this.i18n.t('stock.snack.loadError'), this.i18n.t('common.close'), { duration: 3000 });
      }
    });
  }

  loadMovements(): void {
    this.businessService.getStockMovements(this.movementFilter || undefined).subscribe({
      next: (movements) => {
        this.movements = movements;
      },
      error: (err) => {
        console.error('Erreur lors du chargement des mouvements:', err);
        this.snack.open(this.i18n.t('stock.movements.loadError'), this.i18n.t('common.close'), { duration: 3000 });
      }
    });
  }

  loadForecast(): void {
    this.forecastError = false;
    this.stockService.getForecast({ all: this.showAllForecast }).subscribe({
      next: (result) => {
        this.forecast = result;
      },
      error: () => {
        this.forecastError = true;
        this.forecast = null;
        this.snack.open(this.i18n.t('stock.forecast.loadError'), this.i18n.t('common.close'), { duration: 3000 });
      }
    });
  }

  toggleShowAllForecast(): void {
    this.showAllForecast = !this.showAllForecast;
    this.loadForecast();
  }

  forecastRiskClass(risk: string): string {
    return (risk || 'ok').toLowerCase();
  }

  groupBySupplier(): void {
    const grouped = new Map<string, StockItem[]>();
    const unspecified = this.i18n.t('stock.unspecifiedSupplier');

    this.stockItems.forEach(item => {
      const supplier = item.supplier || unspecified;
      if (!grouped.has(supplier)) {
        grouped.set(supplier, []);
      }
      grouped.get(supplier)!.push(item);
    });

    this.stockBySupplier = Array.from(grouped.entries())
      .map(([supplier, items]) => ({ supplier, items }))
      .sort((a, b) => a.supplier.localeCompare(b.supplier));

    if (this.expandedSuppliers.size === 0 && this.stockBySupplier.length > 0) {
      this.expandedSuppliers.add(this.stockBySupplier[0].supplier);
    }
  }

  toggleSupplier(supplier: string): void {
    if (this.expandedSuppliers.has(supplier)) {
      this.expandedSuppliers.clear();
      return;
    }
    this.expandedSuppliers.clear();
    this.expandedSuppliers.add(supplier);
  }

  isSupplierExpanded(supplier: string): boolean {
    return this.expandedSuppliers.has(supplier);
  }

  onSearch(): void {
    this.loadStock();
  }

  clearSearch(): void {
    this.searchQuery = '';
    this.loadStock();
  }

  onMovementFilter(): void {
    this.loadMovements();
  }

  clearMovementFilter(): void {
    this.movementFilter = '';
    this.loadMovements();
  }

  /** Clic produit stock → onglet Mouvements filtré sur ce code. */
  showProductMovements(productKey: string, event?: Event): void {
    event?.stopPropagation();
    if (!productKey?.trim()) return;
    this.movementFilter = productKey.trim();
    this.selectedTab = 1;
    this.loadMovements();
  }

  reconcileOuts(): void {
    this.stockService.reconcileOuts().subscribe({
      next: (r) => {
        this.snack.open(
          this.i18n.t('stock.reconcileOuts.success', { families: r.familiesFixed, rows: r.rowsTouched }),
          this.i18n.t('common.close'),
          { duration: 4000 }
        );
        this.loadStock();
        this.loadMovements();
        this.loadForecast();
      },
      error: () => {
        this.snack.open(this.i18n.t('stock.reconcileOuts.error'), this.i18n.t('common.close'), { duration: 3000 });
      }
    });
  }

  openAdjustModal(productKey?: string): void {
    this.showAdjustModal = true;
    this.adjustError = '';
    this.newMovement = this.createEmptyMovement();
    if (productKey) {
      this.newMovement.productKey = productKey;
    }
  }

  saveMovement(): void {
    if (!this.newMovement.productKey?.trim()) {
      this.adjustError = this.i18n.t('stock.adjust.productKey');
      return;
    }
    if (!this.newMovement.quantity) {
      this.adjustError = this.i18n.t('stock.adjust.quantity');
      return;
    }

    this.adjustError = '';
    this.businessService.createStockMovement(this.newMovement).subscribe({
      next: () => {
        this.showAdjustModal = false;
        this.snack.open(this.i18n.t('stock.adjust.success'), this.i18n.t('common.close'), { duration: 2500 });
        this.loadStock();
        this.loadMovements();
        this.loadForecast();
      },
      error: (err) => {
        this.adjustError = err?.error?.error || err?.error || this.i18n.t('stock.adjust.error');
      }
    });
  }

  movementTypeLabel(type: string): string {
    return this.i18n.t(`stock.type.${type}` as any) || type;
  }

  formatDate(dateString: string): string {
    if (!dateString) return '-';
    const date = new Date(dateString);
    return date.toLocaleDateString(this.i18n.numberLocale(), {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  getTotalQuantity(): number {
    return this.stockItems.reduce((sum, item) => sum + item.quantityOnHand, 0);
  }

  getSupplierTotalQuantity(items: StockItem[]): number {
    return items.reduce((sum, item) => sum + item.quantityOnHand, 0);
  }

  availableQty(item: StockItem): number {
    return Math.max(0, Number(item.quantityOnHand || 0) - Number(item.reservedQuantity || 0));
  }

  sortedStockItems(items: StockItem[]): StockItem[] {
    void this.stockSort.version;
    return this.stockSort.sort(items, {
      productKey: i => i.productKey,
      description: i => i.description ?? '',
      quantityOnHand: i => i.quantityOnHand,
      reservedQuantity: i => i.reservedQuantity ?? 0,
      available: i => this.availableQty(i),
      averageCost: i => i.averageCost ?? 0,
      unit: i => i.unit ?? '',
      lastUpdated: i => i.lastUpdated ?? ''
    });
  }

  get sortedMovements(): StockMovement[] {
    void this.movementSort.version;
    return this.movementSort.sort(this.movements, {
      createdAt: m => m.createdAt ?? '',
      movementType: m => m.movementType ?? '',
      productKey: m => m.productKey,
      quantity: m => m.quantity,
      unitCost: m => m.unitCost ?? 0,
      stockValue: m => m.stockValue ?? 0,
      reason: m => m.reason ?? '',
      referenceDocument: m => m.referenceDocument ?? '',
      createdBy: m => m.createdBy ?? ''
    });
  }

  runStockAlerts(): void {
    if (!this.perm.has(Permissions.EmailSend)) return;
    this.runningStockAlerts = true;
    this.emailService.runStockAlerts().subscribe({
      next: (r) => {
        this.runningStockAlerts = false;
        this.snack.open(this.i18n.t('stock.alertsDone', { queued: r.queued, skipped: r.skipped }), this.i18n.t('common.close'), { duration: 4000 });
      },
      error: (err) => {
        this.runningStockAlerts = false;
        this.snack.open(err?.error?.error || this.i18n.t('stock.alertsError'), this.i18n.t('common.close'), { duration: 5000 });
      }
    });
  }

  private createEmptyMovement(): StockMovement {
    return {
      productKey: '',
      movementType: 'Adjustment',
      quantity: 0,
      unitCost: null,
      reason: '',
      referenceDocument: ''
    };
  }
}
