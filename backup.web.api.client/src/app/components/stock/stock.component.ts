import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { StockService } from '../../services/stock.service';
import { StockItem } from '../../models/stock-item';
import { MaterialModule } from '../../material.module';
import { MatSnackBar } from '@angular/material/snack-bar';

@Component({
  selector: 'app-stock',
  templateUrl: './stock.component.html',
  styleUrls: ['./stock.component.css'],
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, RouterModule]
})
export class StockComponent implements OnInit {
  stockItems: StockItem[] = [];
  filteredItems: StockItem[] = [];
  stockBySupplier: { supplier: string; items: StockItem[] }[] = [];
  searchQuery: string = '';
  displayedColumns: string[] = ['productKey', 'description', 'quantityOnHand', 'unit', 'lastUpdated'];

  constructor(
    private stockService: StockService,
    private snack: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.loadStock();
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
        this.snack.open('Erreur lors du chargement du stock', 'Fermer', { duration: 3000 });
      }
    });
  }

  groupBySupplier(): void {
    const grouped = new Map<string, StockItem[]>();
    
    this.stockItems.forEach(item => {
      const supplier = item.supplier || 'Non spécifié';
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
  }

  onSearch(): void {
    this.loadStock();
  }

  clearSearch(): void {
    this.searchQuery = '';
    this.loadStock();
  }

  formatDate(dateString: string): string {
    if (!dateString) return '-';
    const date = new Date(dateString);
    return date.toLocaleDateString('fr-FR', {
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
}

