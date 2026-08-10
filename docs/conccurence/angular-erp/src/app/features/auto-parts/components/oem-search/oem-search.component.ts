// ============================================================
// src/app/features/auto-parts/components/oem-search/oem-search.component.ts
// Recherche par numéro OEM
// ============================================================

import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { Observable, of } from 'rxjs';
import { catchError, finalize } from 'rxjs/operators';
import { Product } from '../../../../core/models';
import { AutoPartsService } from '../../services/auto-parts.service';

@Component({
  selector: 'app-oem-search',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="page-header">
      <h1>🔍 Recherche par OEM</h1>
      <p class="subtitle">Entrez un numéro OEM, de fabricant ou de constructeur</p>
    </div>

    <div class="search-panel">
      <div class="oem-input-group">
        <input type="text" [(ngModel)]="oemNumber" placeholder="ex: 0281002937 ou 7700105767"
               class="oem-input" (keyup.enter)="search()" />
        <button (click)="search()" [disabled]="!oemNumber || loading" class="btn-search">
          <span *ngIf="!loading">🔍 Rechercher</span>
          <span *ngIf="loading">⏳ Recherche...</span>
        </button>
      </div>
      <div class="hints">
        <span class="hint-tag" *ngFor="let hint of commonOems" (click)="oemNumber = hint; search()">{{ hint }}</span>
      </div>
    </div>

    <div class="result-card" *ngIf="product">
      <div class="result-header">
        <span class="badge-found">✓ Pièce trouvée</span>
      </div>
      <div class="result-body">
        <img *ngIf="product.images?.length" [src]="product.images![0].url" class="result-img" />
        <div class="result-info">
          <div class="result-brand">{{ product.brand }}</div>
          <h2>{{ product.name }}</h2>
          <div class="result-refs">
            <span class="ref-chip">OEM: <b>{{ oemNumber }}</b></span>
            <span class="ref-chip">Réf: {{ product.reference }}</span>
            <span class="ref-chip">EAN: {{ product.ean }}</span>
          </div>
          <div class="result-price">{{ product.priceHT | number:'1.2-2' }} € HT</div>
          <button [routerLink]="['/products', product.id]" class="btn-detail">
            Voir la fiche complète →
          </button>
        </div>
      </div>
    </div>

    <div class="not-found" *ngIf="notFound">
      ❌ Aucune pièce trouvée pour le numéro <code>{{ oemNumber }}</code>
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 24px; }
    .page-header h1 { font-size: 24px; color: #1a1a2e; margin: 0; }
    .subtitle { color: #888; margin-top: 4px; }
    .search-panel { background: #fff; border-radius: 12px; padding: 24px; box-shadow: 0 2px 8px rgba(0,0,0,0.06); margin-bottom: 24px; }
    .oem-input-group { display: flex; gap: 12px; margin-bottom: 16px; }
    .oem-input { flex: 1; padding: 14px 16px; border: 2px solid #e0e0e0; border-radius: 10px; font-size: 16px; font-family: monospace; outline: none; transition: border-color 0.2s; }
    .oem-input:focus { border-color: #e94560; }
    .btn-search { background: #e94560; color: #fff; border: none; padding: 14px 28px; border-radius: 10px; font-weight: 600; cursor: pointer; transition: opacity 0.2s; white-space: nowrap; }
    .btn-search:hover:not(:disabled) { opacity: 0.9; }
    .btn-search:disabled { opacity: 0.5; cursor: not-allowed; }
    .hints { display: flex; flex-wrap: wrap; gap: 8px; }
    .hint-tag { background: #f0f0f0; color: #666; padding: 6px 12px; border-radius: 16px; font-size: 12px; font-family: monospace; cursor: pointer; transition: all 0.2s; }
    .hint-tag:hover { background: #e94560; color: #fff; }
    .result-card { background: #fff; border-radius: 12px; padding: 24px; box-shadow: 0 4px 16px rgba(0,0,0,0.08); border-left: 4px solid #2e7d32; }
    .result-header { margin-bottom: 16px; }
    .badge-found { background: #e8f5e9; color: #2e7d32; padding: 4px 12px; border-radius: 16px; font-size: 12px; font-weight: 600; }
    .result-body { display: flex; gap: 24px; }
    .result-img { width: 200px; height: 200px; object-fit: contain; background: #f8f9fa; border-radius: 8px; }
    .result-info { flex: 1; }
    .result-brand { font-size: 12px; text-transform: uppercase; color: #e94560; font-weight: 700; }
    .result-info h2 { font-size: 20px; color: #1a1a2e; margin: 8px 0; }
    .result-refs { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 16px; }
    .ref-chip { background: #f0f4ff; color: #667eea; padding: 4px 10px; border-radius: 6px; font-size: 12px; }
    .ref-chip b { font-family: monospace; }
    .result-price { font-size: 24px; font-weight: 700; color: #1a1a2e; margin-bottom: 16px; }
    .btn-detail { background: #1a1a2e; color: #fff; border: none; padding: 10px 20px; border-radius: 8px; font-weight: 600; cursor: pointer; }
    .not-found { background: #ffebee; color: #c62828; padding: 20px; border-radius: 10px; text-align: center; }
    .not-found code { background: #fff; padding: 2px 6px; border-radius: 4px; font-family: monospace; }
  `]
})
export class OemSearchComponent {
  oemNumber = '';
  product?: Product;
  loading = false;
  notFound = false;
  commonOems = ['0281002937', '0986280411', '7700105767', '8200435691'];

  constructor(private autoPartsService: AutoPartsService) {}

  search(): void {
    if (!this.oemNumber) return;
    this.loading = true;
    this.notFound = false;
    this.product = undefined;

    this.autoPartsService.searchByOem(this.oemNumber).pipe(
      catchError(err => {
        this.notFound = true;
        return of(undefined);
      }),
      finalize(() => this.loading = false)
    ).subscribe(result => {
      this.product = result;
    });
  }
}
