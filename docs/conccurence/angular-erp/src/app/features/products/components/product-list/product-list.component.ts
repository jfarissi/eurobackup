// ============================================================
// src/app/features/products/components/product-list/product-list.component.ts
// Liste générique de produits (toutes les sociétés)
// ============================================================

import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Observable, BehaviorSubject } from 'rxjs';
import { switchMap, debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { Product } from '../../../../core/models';
import { ProductService } from '../../services/product.service';

@Component({
  selector: 'app-product-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  template: `
    <div class="page-header">
      <h1>📦 Catalogue Produits</h1>
      <div class="search-box">
        <input type="text" [(ngModel)]="searchQuery" (ngModelChange)="search$.next($event)"
               placeholder="Rechercher par nom, référence ou EAN..." class="search-input" />
        <span class="search-icon">🔍</span>
      </div>
    </div>

    <div class="products-grid">
      <div *ngFor="let product of products$ | async" class="product-card" [routerLink]="['/products', product.id]">
        <div class="product-image">
          <img *ngIf="product.images?.length" [src]="product.images![0].url" [alt]="product.name" />
          <div *ngIf="!product.images?.length" class="no-image">🖼️</div>
        </div>
        <div class="product-info">
          <div class="product-brand">{{ product.brand || 'Sans marque' }}</div>
          <h3 class="product-name">{{ product.name || 'Produit sans nom' }}</h3>
          <div class="product-ref">Réf: {{ product.reference || 'N/A' }}</div>
          <div class="product-meta">
            <span class="price">{{ product.priceHT | number:'1.2-2' }} € HT</span>
            <span class="stock" [class.low]="(product.stockQuantity || 0) < 10">
              Stock: {{ product.stockQuantity | number }}
            </span>
          </div>
          <!-- Dimensions (si renseignées) -->
          <div class="dimensions" *ngIf="product.weight || product.height">
            <span *ngIf="product.weight">⚖️ {{ product.weight }} kg</span>
            <span *ngIf="product.height">📐 {{ product.height }}×{{ product.width }}×{{ product.depth }} cm</span>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    .page-header h1 { font-size: 24px; color: #1a1a2e; margin: 0; }
    .search-box { position: relative; width: 400px; }
    .search-input {
      width: 100%; padding: 10px 16px 10px 40px; border: 1px solid #ddd;
      border-radius: 8px; font-size: 14px; outline: none;
    }
    .search-input:focus { border-color: #e94560; }
    .search-icon { position: absolute; left: 12px; top: 50%; transform: translateY(-50%); }
    .products-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 20px; }
    .product-card {
      background: #fff; border-radius: 12px; overflow: hidden;
      box-shadow: 0 2px 8px rgba(0,0,0,0.06); cursor: pointer;
      transition: transform 0.2s, box-shadow 0.2s;
    }
    .product-card:hover { transform: translateY(-4px); box-shadow: 0 8px 24px rgba(0,0,0,0.12); }
    .product-image { height: 180px; background: #f5f5f5; display: flex; align-items: center; justify-content: center; }
    .product-image img { width: 100%; height: 100%; object-fit: cover; }
    .no-image { font-size: 48px; opacity: 0.3; }
    .product-info { padding: 16px; }
    .product-brand { font-size: 11px; text-transform: uppercase; color: #e94560; font-weight: 700; letter-spacing: 0.5px; }
    .product-name { font-size: 15px; font-weight: 600; color: #1a1a2e; margin: 4px 0; line-height: 1.3; }
    .product-ref { font-size: 12px; color: #888; margin-bottom: 8px; }
    .product-meta { display: flex; justify-content: space-between; align-items: center; }
    .price { font-size: 16px; font-weight: 700; color: #1a1a2e; }
    .stock { font-size: 12px; color: #2e7d32; background: #e8f5e9; padding: 2px 8px; border-radius: 4px; }
    .stock.low { color: #c62828; background: #ffebee; }
    .dimensions { margin-top: 8px; display: flex; gap: 12px; font-size: 11px; color: #666; }
  `]
})
export class ProductListComponent implements OnInit {
  products$!: Observable<Product[]>;
  searchQuery = '';
  search$ = new BehaviorSubject<string>('');

  constructor(private productService: ProductService) {}

  ngOnInit(): void {
    this.products$ = this.search$.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap(query =>
        query.length > 2
          ? this.productService.search(query)
          : this.productService.getAll()
      )
    );
  }
}
