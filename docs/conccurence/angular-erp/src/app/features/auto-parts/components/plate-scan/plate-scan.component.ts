// ============================================================
// src/app/features/auto-parts/components/plate-scan/plate-scan.component.ts
// Composant principal : scan photo + résultats véhicule + pièces compatibles
// ============================================================

import { Component, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { finalize } from 'rxjs';
import { PlateScanService, PlateScanResult, PlateHistoryItem } from '../../services/plate-scan.service';

@Component({
  selector: 'app-plate-scan',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="page-header">
      <h1>📸 Recherche par Plaque d'immatriculation</h1>
      <p class="subtitle">Prenez une photo de la plaque ou saisissez le numéro manuellement</p>
    </div>

    <div class="scan-container">
      <!-- Zone de scan -->
      <div class="scan-panel">
        <div class="scan-tabs">
          <button [class.active]="mode === 'camera'" (click)="mode = 'camera'">📷 Photo</button>
          <button [class.active]="mode === 'manual'" (click)="mode = 'manual'">⌨️ Saisie manuelle</button>
          <button [class.active]="mode === 'vin'" (click)="mode = 'vin'">🔢 VIN</button>
        </div>

        <!-- Mode Photo -->
        <div *ngIf="mode === 'camera'" class="scan-zone">
          <div class="drop-zone" 
               (dragover)="onDragOver($event)" 
               (dragleave)="onDragLeave($event)"
               (drop)="onDrop($event)"
               [class.dragover]="isDragging"
               (click)="fileInput.click()">
            <input #fileInput type="file" accept="image/*" capture="environment" 
                   (change)="onFileSelected($event)" hidden />
            <div class="drop-content">
              <div class="drop-icon">📷</div>
              <p><b>Cliquez ou glissez une photo de plaque</b></p>
              <p class="hint">Formats acceptés : JPG, PNG, WEBP — Max 5Mo</p>
              <p class="hint-mobile">📱 Sur mobile : activez la caméra arrière</p>
            </div>
          </div>
          <div class="preview" *ngIf="previewUrl">
            <img [src]="previewUrl" alt="Aperçu plaque" />
            <button class="btn-clear" (click)="clearPreview()">✕</button>
          </div>
          <button *ngIf="previewUrl" (click)="scanImage()" [disabled]="scanning" class="btn-scan">
            <span *ngIf="!scanning">🔍 Analyser la plaque</span>
            <span *ngIf="scanning">
              <span class="spinner"></span> Analyse en cours...
            </span>
          </button>
        </div>

        <!-- Mode Manuel -->
        <div *ngIf="mode === 'manual'" class="scan-zone">
          <div class="form-group">
            <label>Numéro de plaque</label>
            <input type="text" [(ngModel)]="manualPlate" 
                   placeholder="ex: AB-123-CD ou 1-ABC-234" 
                   class="plate-input"
                   (keyup.enter)="searchManual()" />
          </div>
          <div class="form-group">
            <label>Pays (optionnel)</label>
            <select [(ngModel)]="manualCountry" class="plate-input">
              <option value="">Auto-détecté</option>
              <option value="FR">🇫🇷 France</option>
              <option value="BE">🇧🇪 Belgique</option>
              <option value="DE">🇩🇪 Allemagne</option>
              <option value="NL">🇳🇱 Pays-Bas</option>
              <option value="LU">🇱🇺 Luxembourg</option>
            </select>
          </div>
          <button (click)="searchManual()" [disabled]="!manualPlate || scanning" class="btn-scan">
            <span *ngIf="!scanning">🔍 Rechercher</span>
            <span *ngIf="scanning"><span class="spinner"></span> Recherche...</span>
          </button>
        </div>

        <!-- Mode VIN -->
        <div *ngIf="mode === 'vin'" class="scan-zone">
          <div class="form-group">
            <label>Numéro VIN (17 caractères)</label>
            <input type="text" [(ngModel)]="manualVin" 
                   placeholder="ex: VF1BZ0L0632345678" 
                   class="plate-input"
                   maxlength="17"
                   (keyup.enter)="searchVin()" />
            <p class="hint">Le VIN se trouve sur la carte grise ou le pare-brise</p>
          </div>
          <button (click)="searchVin()" [disabled]="manualVin.length < 17 || scanning" class="btn-scan">
            <span *ngIf="!scanning">🔍 Décoder le VIN</span>
            <span *ngIf="scanning"><span class="spinner"></span> Décodage...</span>
          </button>
        </div>

        <div class="error-msg" *ngIf="error">❌ {{ error }}</div>
      </div>

      <!-- Historique -->
      <div class="history-panel" *ngIf="history.length">
        <h3>🕐 Recherches récentes</h3>
        <div class="history-list">
          <div *ngFor="let h of history" class="history-item" (click)="replaySearch(h)">
            <span class="h-plate">{{ h.plateNumber }}</span>
            <span class="h-vehicle">{{ h.make }} {{ h.model }} {{ h.year }}</span>
            <span class="h-count">{{ h.productsFound }} pièces</span>
            <span class="h-date">{{ h.searchedAt | date:'short' }}</span>
          </div>
        </div>
      </div>
    </div>

    <!-- Résultats Véhicule -->
    <div class="vehicle-result" *ngIf="result">
      <div class="vehicle-card">
        <div class="vehicle-header">
          <div class="plate-badge">{{ result.plateNumber }}</div>
          <h2>{{ result.make }} {{ result.model }} <span *ngIf="result.year">({{ result.year }})</span></h2>
        </div>
        <div class="vehicle-specs">
          <div class="spec" *ngIf="result.vin"><span>VIN</span><code>{{ result.vin }}</code></div>
          <div class="spec" *ngIf="result.engineCode"><span>Moteur</span><b>{{ result.engineCode }}</b></div>
          <div class="spec" *ngIf="result.fuelType"><span>Carburant</span><b>{{ result.fuelType }}</b></div>
          <div class="spec" *ngIf="result.powerHP"><span>Puissance</span><b>{{ result.powerHP }} CV</b></div>
        </div>
      </div>

      <!-- Pièces compatibles -->
      <div class="products-section">
        <div class="section-header">
          <h3>📦 {{ result.compatibleProducts.length }} pièce(s) compatible(s)</h3>
          <div class="category-filters">
            <button *ngFor="let cat of categories" 
                    [class.active]="selectedCategory === cat"
                    (click)="selectedCategory = cat"
                    class="filter-chip">
              {{ cat }}
            </button>
          </div>
        </div>

        <div class="products-grid">
          <div *ngFor="let p of filteredProducts" class="product-card" [routerLink]="['/products', p.id]">
            <div class="product-img">
              <img *ngIf="p.imageUrl" [src]="p.imageUrl" [alt]="p.name" />
              <div *ngIf="!p.imageUrl" class="no-img">🖼️</div>
            </div>
            <div class="product-info">
              <div class="p-brand">{{ p.brand }}</div>
              <div class="p-name">{{ p.name }}</div>
              <div class="p-ref">{{ p.reference }}</div>
              <div class="p-cat">{{ p.categoryName }}</div>
              <div class="p-footer">
                <span class="p-price">{{ p.priceHT | number:'1.2-2' }} €</span>
                <span class="p-stock" [class.low]="(p.stockQuantity || 0) < 5">
                  {{ p.stockQuantity || 0 }} en stock
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 24px; }
    .page-header h1 { font-size: 24px; color: #1a1a2e; margin: 0; }
    .subtitle { color: #888; margin-top: 4px; }
    .scan-container { display: grid; grid-template-columns: 1fr 320px; gap: 24px; margin-bottom: 32px; }
    .scan-panel { background: #fff; border-radius: 16px; padding: 24px; box-shadow: 0 2px 12px rgba(0,0,0,0.06); }
    .scan-tabs { display: flex; gap: 8px; margin-bottom: 20px; border-bottom: 2px solid #f0f0f0; padding-bottom: 12px; }
    .scan-tabs button { background: none; border: none; padding: 8px 16px; border-radius: 8px; font-size: 14px; font-weight: 600; color: #888; cursor: pointer; transition: all 0.2s; }
    .scan-tabs button.active { background: #e94560; color: #fff; }
    .scan-tabs button:hover:not(.active) { background: #f5f5f5; color: #444; }
    .scan-zone { min-height: 200px; }
    .drop-zone { border: 3px dashed #ddd; border-radius: 16px; padding: 40px; text-align: center; cursor: pointer; transition: all 0.2s; background: #fafafa; }
    .drop-zone.dragover { border-color: #e94560; background: #fff0f2; }
    .drop-icon { font-size: 48px; margin-bottom: 12px; }
    .drop-content p { margin: 4px 0; color: #444; }
    .drop-content .hint { font-size: 12px; color: #888; }
    .drop-content .hint-mobile { font-size: 12px; color: #667eea; }
    .preview { position: relative; margin: 16px 0; }
    .preview img { max-width: 100%; max-height: 300px; border-radius: 12px; box-shadow: 0 4px 12px rgba(0,0,0,0.1); }
    .btn-clear { position: absolute; top: 8px; right: 8px; background: rgba(0,0,0,0.6); color: #fff; border: none; width: 32px; height: 32px; border-radius: 50%; cursor: pointer; font-size: 16px; }
    .form-group { margin-bottom: 16px; }
    .form-group label { display: block; font-size: 12px; font-weight: 600; color: #666; text-transform: uppercase; margin-bottom: 6px; }
    .plate-input { width: 100%; padding: 14px 16px; border: 2px solid #e0e0e0; border-radius: 12px; font-size: 18px; font-family: monospace; letter-spacing: 2px; text-transform: uppercase; outline: none; transition: border-color 0.2s; }
    .plate-input:focus { border-color: #e94560; }
    .btn-scan { width: 100%; background: #e94560; color: #fff; border: none; padding: 16px; border-radius: 12px; font-size: 16px; font-weight: 700; cursor: pointer; transition: opacity 0.2s; margin-top: 8px; }
    .btn-scan:hover:not(:disabled) { opacity: 0.9; }
    .btn-scan:disabled { opacity: 0.5; cursor: not-allowed; }
    .spinner { display: inline-block; width: 16px; height: 16px; border: 2px solid rgba(255,255,255,0.3); border-top-color: #fff; border-radius: 50%; animation: spin 0.8s linear infinite; margin-right: 8px; }
    @keyframes spin { to { transform: rotate(360deg); } }
    .error-msg { background: #ffebee; color: #c62828; padding: 12px 16px; border-radius: 8px; margin-top: 12px; font-size: 14px; }
    .history-panel { background: #fff; border-radius: 16px; padding: 20px; box-shadow: 0 2px 12px rgba(0,0,0,0.06); }
    .history-panel h3 { font-size: 14px; color: #666; margin: 0 0 12px; text-transform: uppercase; }
    .history-list { display: flex; flex-direction: column; gap: 8px; }
    .history-item { padding: 10px 12px; background: #f8f9fa; border-radius: 8px; cursor: pointer; transition: all 0.2s; font-size: 13px; }
    .history-item:hover { background: #e3f2fd; }
    .h-plate { font-family: monospace; font-weight: 700; color: #e94560; display: block; }
    .h-vehicle { color: #444; }
    .h-count { color: #2e7d32; font-size: 11px; }
    .h-date { color: #888; font-size: 11px; float: right; }
    .vehicle-result { margin-top: 24px; }
    .vehicle-card { background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%); color: #fff; border-radius: 16px; padding: 24px; margin-bottom: 24px; }
    .vehicle-header { display: flex; align-items: center; gap: 16px; margin-bottom: 16px; }
    .plate-badge { background: #e94560; padding: 8px 16px; border-radius: 8px; font-family: monospace; font-size: 18px; font-weight: 700; }
    .vehicle-header h2 { margin: 0; font-size: 22px; }
    .vehicle-header h2 span { opacity: 0.7; font-size: 16px; }
    .vehicle-specs { display: flex; gap: 24px; flex-wrap: wrap; }
    .spec { display: flex; flex-direction: column; gap: 2px; }
    .spec span { font-size: 11px; text-transform: uppercase; opacity: 0.6; }
    .spec b, .spec code { font-size: 14px; }
    .spec code { background: rgba(255,255,255,0.1); padding: 2px 8px; border-radius: 4px; }
    .products-section { background: #fff; border-radius: 16px; padding: 24px; box-shadow: 0 2px 12px rgba(0,0,0,0.06); }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; flex-wrap: wrap; gap: 12px; }
    .section-header h3 { margin: 0; font-size: 16px; color: #1a1a2e; }
    .category-filters { display: flex; gap: 8px; flex-wrap: wrap; }
    .filter-chip { background: #f0f0f0; border: none; padding: 6px 14px; border-radius: 20px; font-size: 12px; color: #666; cursor: pointer; transition: all 0.2s; }
    .filter-chip.active { background: #e94560; color: #fff; }
    .filter-chip:hover:not(.active) { background: #e0e0e0; }
    .products-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 16px; }
    .product-card { background: #fff; border: 1px solid #eee; border-radius: 12px; overflow: hidden; cursor: pointer; transition: all 0.2s; }
    .product-card:hover { box-shadow: 0 8px 24px rgba(0,0,0,0.1); transform: translateY(-4px); }
    .product-img { height: 160px; background: #f5f5f5; display: flex; align-items: center; justify-content: center; }
    .product-img img { width: 100%; height: 100%; object-fit: cover; }
    .no-img { font-size: 48px; opacity: 0.3; }
    .product-info { padding: 14px; }
    .p-brand { font-size: 10px; text-transform: uppercase; color: #e94560; font-weight: 700; letter-spacing: 0.5px; }
    .p-name { font-size: 14px; font-weight: 600; color: #1a1a2e; margin: 4px 0; line-height: 1.3; }
    .p-ref { font-size: 11px; color: #888; font-family: monospace; }
    .p-cat { font-size: 11px; color: #667eea; margin-top: 4px; }
    .p-footer { display: flex; justify-content: space-between; align-items: center; margin-top: 10px; }
    .p-price { font-size: 16px; font-weight: 700; color: #1a1a2e; }
    .p-stock { font-size: 11px; color: #2e7d32; background: #e8f5e9; padding: 2px 8px; border-radius: 4px; }
    .p-stock.low { color: #c62828; background: #ffebee; }
    @media (max-width: 768px) {
      .scan-container { grid-template-columns: 1fr; }
      .vehicle-specs { gap: 12px; }
      .section-header { flex-direction: column; align-items: flex-start; }
    }
  `]
})
export class PlateScanComponent {
  @ViewChild('fileInput') fileInput!: ElementRef<HTMLInputElement>;

  mode: 'camera' | 'manual' | 'vin' = 'camera';
  isDragging = false;
  previewUrl?: string;
  selectedFile?: File;
  scanning = false;
  error = '';

  manualPlate = '';
  manualCountry = '';
  manualVin = '';

  result?: PlateScanResult;
  history: PlateHistoryItem[] = [];
  selectedCategory = 'Toutes';

  constructor(private plateService: PlateScanService) {
    this.loadHistory();
  }

  get categories(): string[] {
    if (!this.result) return [];
    const cats = ['Toutes', ...new Set(this.result.compatibleProducts.map(p => p.categoryName).filter(Boolean))];
    return cats;
  }

  get filteredProducts() {
    if (!this.result) return [];
    if (this.selectedCategory === 'Toutes') return this.result.compatibleProducts;
    return this.result.compatibleProducts.filter(p => p.categoryName === this.selectedCategory);
  }

  // ── Drag & Drop ──
  onDragOver(e: DragEvent): void {
    e.preventDefault();
    this.isDragging = true;
  }
  onDragLeave(e: DragEvent): void {
    e.preventDefault();
    this.isDragging = false;
  }
  onDrop(e: DragEvent): void {
    e.preventDefault();
    this.isDragging = false;
    const files = e.dataTransfer?.files;
    if (files?.length) this.handleFile(files[0]);
  }

  onFileSelected(e: Event): void {
    const file = (e.target as HTMLInputElement).files?.[0];
    if (file) this.handleFile(file);
  }

  handleFile(file: File): void {
    if (!file.type.startsWith('image/')) {
      this.error = 'Veuillez sélectionner une image';
      return;
    }
    if (file.size > 5 * 1024 * 1024) {
      this.error = 'Image trop grande (max 5Mo)';
      return;
    }
    this.error = '';
    this.selectedFile = file;
    this.previewUrl = URL.createObjectURL(file);
  }

  clearPreview(): void {
    this.previewUrl = undefined;
    this.selectedFile = undefined;
    this.result = undefined;
    if (this.fileInput) this.fileInput.nativeElement.value = '';
  }

  // ── Scan ──
  scanImage(): void {
    if (!this.selectedFile) return;
    this.scanning = true;
    this.error = '';

    this.plateService.scanPlate(this.selectedFile).pipe(
      finalize(() => this.scanning = false)
    ).subscribe({
      next: res => { this.result = res; this.loadHistory(); },
      error: err => { this.error = err.error?.message || 'Erreur lors de l'analyse de la plaque'; }
    });
  }

  searchManual(): void {
    if (!this.manualPlate) return;
    this.scanning = true;
    this.error = '';

    this.plateService.searchByPlate(this.manualPlate, this.manualCountry || undefined).pipe(
      finalize(() => this.scanning = false)
    ).subscribe({
      next: res => { this.result = res; this.loadHistory(); },
      error: err => { this.error = err.error?.message || 'Plaque non trouvée'; }
    });
  }

  searchVin(): void {
    if (this.manualVin.length < 17) return;
    this.scanning = true;
    this.error = '';

    this.plateService.searchByVin(this.manualVin).pipe(
      finalize(() => this.scanning = false)
    ).subscribe({
      next: res => { this.result = res; this.loadHistory(); },
      error: err => { this.error = err.error?.message || 'VIN non trouvé'; }
    });
  }

  replaySearch(h: PlateHistoryItem): void {
    this.manualPlate = h.plateNumber;
    this.mode = 'manual';
    this.searchManual();
  }

  private loadHistory(): void {
    this.plateService.getHistory().subscribe(h => this.history = h.slice(0, 10));
  }
}
