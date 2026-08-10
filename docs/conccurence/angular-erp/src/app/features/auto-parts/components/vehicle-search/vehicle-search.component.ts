// ============================================================
// src/app/features/auto-parts/components/vehicle-search/vehicle-search.component.ts
// Recherche de pièces par véhicule (Marque/Modèle/Année)
// ============================================================

import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { Observable } from 'rxjs';
import { Product } from '../../../../core/models';
import { AutoPartsService } from '../../services/auto-parts.service';

@Component({
  selector: 'app-vehicle-search',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="page-header">
      <h1>🚗 Recherche par Véhicule</h1>
      <p class="subtitle">Trouvez les pièces compatibles avec un véhicule</p>
    </div>

    <div class="search-panel">
      <div class="form-row">
        <div class="form-group">
          <label>Marque</label>
          <select [(ngModel)]="make" (ngModelChange)="onMakeChange()" class="form-select">
            <option value="">Sélectionner...</option>
            <option *ngFor="let m of makes" [value]="m">{{ m }}</option>
          </select>
        </div>
        <div class="form-group">
          <label>Modèle</label>
          <select [(ngModel)]="model" [disabled]="!make" class="form-select">
            <option value="">Sélectionner...</option>
            <option *ngFor="let m of models" [value]="m">{{ m }}</option>
          </select>
        </div>
        <div class="form-group" style="width: 120px;">
          <label>Année</label>
          <input type="number" [(ngModel)]="year" placeholder="ex: 2018" class="form-input" />
        </div>
        <div class="form-group" style="display: flex; align-items: flex-end;">
          <button (click)="search()" [disabled]="!make || !model" class="btn-primary">
            🔍 Rechercher
          </button>
        </div>
      </div>
    </div>

    <div class="results" *ngIf="results$ | async as results">
      <div class="results-header">
        <span>{{ results.length }} pièce(s) trouvée(s)</span>
        <span *ngIf="make && model" class="vehicle-tag">{{ make }} {{ model }} {{ year ? '(' + year + ')' : '' }}</span>
      </div>

      <div class="products-list">
        <div *ngFor="let p of results" class="product-row" [routerLink]="['/products', p.id]">
          <img *ngIf="p.images?.length" [src]="p.images![0].url" class="row-thumb" />
          <div *ngIf="!p.images?.length" class="row-thumb no-img">🖼️</div>
          <div class="row-info">
            <div class="row-brand">{{ p.brand }}</div>
            <div class="row-name">{{ p.name }}</div>
            <div class="row-ref">{{ p.reference }} | {{ p.ean }}</div>
          </div>
          <div class="row-price">{{ p.priceHT | number:'1.2-2' }} €</div>
          <div class="row-stock" [class.low]="(p.stockQuantity || 0) < 10">
            {{ p.stockQuantity | number }} en stock
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 24px; }
    .page-header h1 { font-size: 24px; color: #1a1a2e; margin: 0; }
    .subtitle { color: #888; margin-top: 4px; }
    .search-panel { background: #fff; border-radius: 12px; padding: 24px; box-shadow: 0 2px 8px rgba(0,0,0,0.06); margin-bottom: 24px; }
    .form-row { display: flex; gap: 16px; align-items: flex-end; }
    .form-group { flex: 1; display: flex; flex-direction: column; gap: 6px; }
    label { font-size: 12px; font-weight: 600; color: #666; text-transform: uppercase; }
    .form-select, .form-input {
      padding: 10px 12px; border: 1px solid #ddd; border-radius: 8px;
      font-size: 14px; outline: none; background: #fff;
    }
    .form-select:focus, .form-input:focus { border-color: #e94560; }
    .btn-primary {
      background: #e94560; color: #fff; border: none; padding: 10px 24px;
      border-radius: 8px; font-weight: 600; cursor: pointer; transition: opacity 0.2s;
    }
    .btn-primary:hover:not(:disabled) { opacity: 0.9; }
    .btn-primary:disabled { opacity: 0.5; cursor: not-allowed; }
    .results-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    .vehicle-tag { background: #e3f2fd; color: #1565c0; padding: 4px 12px; border-radius: 16px; font-size: 13px; font-weight: 600; }
    .products-list { display: flex; flex-direction: column; gap: 12px; }
    .product-row {
      display: flex; align-items: center; gap: 16px;
      background: #fff; padding: 16px; border-radius: 10px;
      box-shadow: 0 1px 4px rgba(0,0,0,0.04); cursor: pointer;
      transition: all 0.2s;
    }
    .product-row:hover { box-shadow: 0 4px 12px rgba(0,0,0,0.08); transform: translateX(4px); }
    .row-thumb { width: 60px; height: 60px; object-fit: cover; border-radius: 8px; background: #f5f5f5; display: flex; align-items: center; justify-content: center; font-size: 24px; }
    .row-thumb.no-img { opacity: 0.3; }
    .row-info { flex: 1; }
    .row-brand { font-size: 11px; text-transform: uppercase; color: #e94560; font-weight: 700; }
    .row-name { font-size: 15px; font-weight: 600; color: #1a1a2e; }
    .row-ref { font-size: 12px; color: #888; }
    .row-price { font-size: 16px; font-weight: 700; color: #1a1a2e; white-space: nowrap; }
    .row-stock { white-space: nowrap; font-size: 12px; color: #2e7d32; background: #e8f5e9; padding: 4px 10px; border-radius: 4px; }
    .row-stock.low { color: #c62828; background: #ffebee; }
  `]
})
export class VehicleSearchComponent {
  make = '';
  model = '';
  year?: number;
  makes = ['Audi', 'BMW', 'Citroën', 'Ford', 'Mercedes', 'Peugeot', 'Renault', 'Volkswagen'];
  models: string[] = [];
  results$!: Observable<Product[]>;

  private modelsByMake: Record<string, string[]> = {
    'Renault': ['Clio', 'Mégane', 'Captur', 'Scénic', 'Talisman'],
    'Peugeot': ['208', '308', '3008', '5008', '508'],
    'Citroën': ['C3', 'C4', 'C5', 'Berlingo'],
    'BMW': ['Série 1', 'Série 3', 'Série 5', 'X1', 'X3', 'X5'],
    'Audi': ['A1', 'A4', 'A6', 'Q3', 'Q5'],
    'Mercedes': ['Classe A', 'Classe C', 'Classe E', 'GLA', 'GLC'],
    'Volkswagen': ['Golf', 'Polo', 'Tiguan', 'Passat'],
    'Ford': ['Fiesta', 'Focus', 'Kuga', 'Puma'],
  };

  constructor(private autoPartsService: AutoPartsService) {}

  onMakeChange(): void {
    this.model = '';
    this.models = this.modelsByMake[this.make] || [];
  }

  search(): void {
    if (!this.make || !this.model) return;
    this.results$ = this.autoPartsService.searchByVehicle(this.make, this.model, this.year);
  }
}
