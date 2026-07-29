import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { StockService } from '../../services/stock.service';
import { BusinessService } from '../../services/business.service';
import { StockItem } from '../../models/stock-item';
import { StockMovement } from '../../models/business';
import { MaterialModule } from '../../material.module';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AppI18nService } from '../../services/app-i18n.service';
import { TPipe } from '../../pipes/t.pipe';
import { PermissionService } from '../../services/permission.service';
import { Permissions } from '../../constants/permissions';

@Component({
  selector: 'app-stock',
  templateUrl: './stock.component.html',
  styleUrls: ['./stock.component.css'],
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, RouterModule, TPipe]
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

  showAdjustModal = false;
  adjustError = '';
  readonly P = Permissions;
  newMovement: StockMovement = this.createEmptyMovement();

  constructor(
    private stockService: StockService,
    private businessService: BusinessService,
    private snack: MatSnackBar,
    private i18n: AppI18nService,
    public perm: PermissionService
  ) {}

  ngOnInit(): void {
    this.loadStock();
    this.loadMovements();
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
      .map(([supplier, items]) => ({
        supplier,
        items: items.sort((a, b) => a.productKey.localeCompare(b.productKey))
      }))
      .sort((a, b) => a.supplier.localeCompare(b.supplier));

    if (this.expandedSuppliers.size === 0 && this.stockBySupplier.length > 0) {
      this.expandedSuppliers.add(this.stockBySupplier[0].supplier);
    }
  }

  toggleSupplier(supplier: string): void {
    if (this.expandedSuppliers.has(supplier)) {
      this.expandedSuppliers.delete(supplier);
    } else {
      this.expandedSuppliers.add(supplier);
    }
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

  private createEmptyMovement(): StockMovement {
    return {
      productKey: '',
      movementType: 'Adjustment',
      quantity: 0,
      reason: '',
      referenceDocument: ''
    };
  }
}
