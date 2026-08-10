// ============================================================
// src/app/features/products/components/product-detail/product-detail.component.ts
// Détail produit — affiche les données spécifiques au module si actif
// ============================================================

import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { Observable, switchMap } from 'rxjs';
import { Product } from '../../../../core/models';
import { ProductService } from '../../services/product.service';
import { ModuleService } from '../../../../core/services/module.service';

@Component({
  selector: 'app-product-detail',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="product-detail" *ngIf="product$ | async as product">
      <div class="detail-header">
        <div class="gallery">
          <img *ngIf="mainImage" [src]="mainImage.url" class="main-image" />
          <div class="thumbnails">
            <img *ngFor="let img of product.images" [src]="img.url"
                 [class.active]="img === mainImage" (click)="mainImage = img" />
          </div>
        </div>
        <div class="info">
          <div class="brand">{{ product.brand }}</div>
          <h1>{{ product.name }}</h1>
          <div class="ref">Référence: <code>{{ product.reference }}</code> | EAN: <code>{{ product.ean }}</code></div>
          <div class="price-row">
            <span class="price">{{ product.priceHT | number:'1.2-2' }} € HT</span>
            <span class="stock-badge" [class.low]="(product.stockQuantity || 0) < 10">
              Stock: {{ product.stockQuantity | number }}
            </span>
          </div>

          <!-- Dimensions -->
          <div class="specs-card" *ngIf="product.weight || product.height">
            <h4>📐 Dimensions & Poids</h4>
            <div class="specs-grid">
              <div *ngIf="product.weight"><span>Poids</span><b>{{ product.weight }} kg</b></div>
              <div *ngIf="product.height"><span>Hauteur</span><b>{{ product.height }} cm</b></div>
              <div *ngIf="product.width"><span>Largeur</span><b>{{ product.width }} cm</b></div>
              <div *ngIf="product.depth"><span>Profondeur</span><b>{{ product.depth }} cm</b></div>
            </div>
          </div>
        </div>
      </div>

      <!-- SECTION PIÈCES AUTO (visible uniquement si module actif) -->
      <div *requireModule="'auto_parts'" class="module-section auto-parts">
        <h2>🚗 Compatibilité Véhicule</h2>
        <div class="vehicles-table" *ngIf="product.vehicles?.length">
          <table>
            <thead><tr><th>Marque</th><th>Modèle</th><th>Années</th><th>Moteur</th></tr></thead>
            <tbody>
              <tr *ngFor="let v of product.vehicles">
                <td>{{ v.make }}</td>
                <td>{{ v.model }}</td>
                <td>{{ v.yearFrom }} - {{ v.yearTo || '...' }}</td>
                <td><code>{{ v.engineCode || '—' }}</code></td>
              </tr>
            </tbody>
          </table>
        </div>
        <div *ngIf="!product.vehicles?.length" class="empty">Aucune donnée de compatibilité</div>

        <h2>🔗 Cross-références OEM</h2>
        <div class="oem-list" *ngIf="product.oemNumbers?.length">
          <span *ngFor="let oem of product.oemNumbers" class="oem-chip"
                [class.original]="oem.isOriginal">
            {{ oem.oemNumber }}
            <small *ngIf="oem.brand">({{ oem.brand }})</small>
          </span>
        </div>
        <div *ngIf="!product.oemNumbers?.length" class="empty">Aucune cross-référence</div>
      </div>
    </div>
  `,
  styles: [`
    .product-detail { max-width: 1200px; margin: 0 auto; }
    .detail-header { display: grid; grid-template-columns: 1fr 1fr; gap: 32px; margin-bottom: 32px; }
    .gallery { background: #fff; border-radius: 12px; padding: 16px; box-shadow: 0 2px 8px rgba(0,0,0,0.06); }
    .main-image { width: 100%; height: 350px; object-fit: contain; border-radius: 8px; background: #f8f9fa; }
    .thumbnails { display: flex; gap: 8px; margin-top: 12px; }
    .thumbnails img { width: 60px; height: 60px; object-fit: cover; border-radius: 6px; cursor: pointer; opacity: 0.6; border: 2px solid transparent; }
    .thumbnails img.active { opacity: 1; border-color: #e94560; }
    .info { padding: 8px; }
    .brand { font-size: 12px; text-transform: uppercase; color: #e94560; font-weight: 700; }
    .info h1 { font-size: 24px; color: #1a1a2e; margin: 8px 0; }
    .ref { font-size: 13px; color: #888; margin-bottom: 16px; }
    .ref code { background: #f0f0f0; padding: 2px 6px; border-radius: 4px; font-family: monospace; }
    .price-row { display: flex; align-items: center; gap: 16px; margin-bottom: 20px; }
    .price { font-size: 28px; font-weight: 700; color: #1a1a2e; }
    .stock-badge { padding: 6px 12px; border-radius: 20px; font-size: 13px; font-weight: 600; background: #e8f5e9; color: #2e7d32; }
    .stock-badge.low { background: #ffebee; color: #c62828; }
    .specs-card { background: #f8f9fa; border-radius: 10px; padding: 16px; }
    .specs-card h4 { margin: 0 0 12px; font-size: 13px; color: #666; text-transform: uppercase; }
    .specs-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 12px; }
    .specs-grid div { display: flex; justify-content: space-between; font-size: 14px; }
    .specs-grid span { color: #888; }
    .specs-grid b { color: #1a1a2e; }
    .module-section { background: #fff; border-radius: 12px; padding: 24px; margin-bottom: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.06); }
    .module-section h2 { font-size: 18px; color: #1a1a2e; margin: 0 0 16px; display: flex; align-items: center; gap: 8px; }
    .vehicles-table { overflow-x: auto; }
    table { width: 100%; border-collapse: collapse; font-size: 14px; }
    th { text-align: left; padding: 10px; background: #f8f9fa; color: #666; font-weight: 600; border-bottom: 2px solid #e0e0e0; }
    td { padding: 10px; border-bottom: 1px solid #eee; }
    .oem-list { display: flex; flex-wrap: wrap; gap: 8px; }
    .oem-chip { background: #e3f2fd; color: #1565c0; padding: 6px 12px; border-radius: 16px; font-size: 13px; font-family: monospace; }
    .oem-chip.original { background: #fff3e0; color: #e65100; border: 1px solid #ffcc80; }
    .oem-chip small { font-family: system-ui; opacity: 0.7; margin-left: 4px; }
    .empty { color: #888; font-style: italic; padding: 16px; background: #fafafa; border-radius: 8px; }
  `]
})
export class ProductDetailComponent implements OnInit {
  product$!: Observable<Product>;
  mainImage?: Product['images'][0];
  hasAutoParts$!: Observable<boolean>;

  constructor(
    private route: ActivatedRoute,
    private productService: ProductService,
    private moduleService: ModuleService
  ) {}

  ngOnInit(): void {
    this.hasAutoParts$ = this.moduleService.hasModule('auto_parts');

    this.product$ = this.route.paramMap.pipe(
      switchMap(params => this.productService.getById(Number(params.get('id')))),
    );

    this.product$.subscribe(p => {
      if (p.images?.length) this.mainImage = p.images[0];
    });
  }
}
