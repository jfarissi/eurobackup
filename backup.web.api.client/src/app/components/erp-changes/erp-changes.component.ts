import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MaterialModule } from '../../material.module';
import { ErpProductChange, ErpSyncLog } from '../../models/erp-product';
import { ErpProductService } from '../../services/erp-product.service';

@Component({
  selector: 'app-erp-changes',
  templateUrl: './erp-changes.component.html',
  styleUrls: ['./erp-changes.component.css'],
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, RouterModule]
})
export class ErpChangesComponent implements OnInit {
  changes: ErpProductChange[] = [];
  syncLogs: ErpSyncLog[] = [];
  total = 0;
  page = 1;
  pageSize = 50;
  loading = false;
  syncing = false;
  importing = false;

  unreadOnly = true;
  changeType = '';
  selectedIds = new Set<number>();

  readonly changeTypes = [
    { value: '', label: 'Tous les types' },
    { value: 'Created', label: 'Création' },
    { value: 'Updated', label: 'Modification' },
    { value: 'PriceChanged', label: 'Prix' },
    { value: 'StockChanged', label: 'Stock' },
    { value: 'Deleted', label: 'Suppression' }
  ];

  constructor(
    private erpService: ErpProductService,
    private snack: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.loadChanges();
    this.loadSyncLogs();
  }

  loadChanges(): void {
    this.loading = true;
    this.erpService.getChanges({
      unreadOnly: this.unreadOnly ? true : undefined,
      changeType: this.changeType || undefined,
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
        this.snack.open('Erreur lors du chargement des changements ERP', 'Fermer', { duration: 3500 });
      }
    });
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
      this.snack.open('Sélectionnez au moins un changement', 'Fermer', { duration: 2500 });
      return;
    }
    this.erpService.markChangesRead(ids).subscribe({
      next: () => {
        this.snack.open(`${ids.length} changement(s) marqué(s) comme lu(s)`, 'OK', { duration: 2500 });
        this.loadChanges();
      },
      error: () => this.snack.open('Impossible de marquer comme lu', 'Fermer', { duration: 3000 })
    });
  }

  markAllVisibleRead(): void {
    const ids = this.changes.filter(c => !c.isRead).map(c => c.id);
    if (ids.length === 0) {
      this.snack.open('Aucun changement non lu sur cette page', 'Fermer', { duration: 2500 });
      return;
    }
    this.erpService.markChangesRead(ids).subscribe({
      next: () => {
        this.snack.open(`${ids.length} changement(s) marqué(s) comme lu(s)`, 'OK', { duration: 2500 });
        this.loadChanges();
      },
      error: () => this.snack.open('Impossible de marquer comme lu', 'Fermer', { duration: 3000 })
    });
  }

  triggerSyncAll(): void {
    if (this.syncing) return;
    this.syncing = true;
    this.snack.open('Synchronisation ERP démarrée…', undefined, { duration: 2500 });
    this.erpService.syncAll().subscribe({
      next: (log) => {
        this.syncing = false;
        this.snack.open(
          `Sync terminée: ${log.status} — +${log.newProducts} / ~${log.updatedProducts} / !${log.failedProducts}`,
          'OK',
          { duration: 5000 }
        );
        this.loadChanges();
        this.loadSyncLogs();
      },
      error: () => {
        this.syncing = false;
        this.snack.open('Échec de la synchronisation ERP', 'Fermer', { duration: 4000 });
      }
    });
  }

  importExcel(syncAfter = false): void {
    if (this.importing) return;
    this.importing = true;
    this.snack.open(
      syncAfter ? 'Import Excel + sync ERP…' : 'Import Excel en cours…',
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
          'OK',
          { duration: 6000 }
        );
        this.loadChanges();
        this.loadSyncLogs();
      },
      error: () => {
        this.importing = false;
        this.snack.open('Échec de l\'import Excel', 'Fermer', { duration: 4000 });
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
    return date.toLocaleString('fr-FR', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  changeTypeLabel(type: string): string {
    return this.changeTypes.find(t => t.value === type)?.label ?? type;
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
      UnitPrice: 'Prix unitaire',
      PriceHT: 'Prix HT',
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

  unreadCount(): number {
    return this.changes.filter(c => !c.isRead).length;
  }
}
