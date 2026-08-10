// ============================================================
// src/app/features/auto-parts/components/sync-panel/sync-panel.component.ts
// Panneau de synchronisation catalogue
// ============================================================

import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { AutoPartsModuleConfig, SyncResult } from '../../../../core/models';
import { AutoPartsService } from '../../services/auto-parts.service';
import { ModuleService } from '../../../../core/services/module.service';

@Component({
  selector: 'app-sync-panel',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="page-header">
      <h1>🔄 Synchronisation Catalogue</h1>
      <p class="subtitle">Mettez à jour votre base depuis le fournisseur de données</p>
    </div>

    <div class="config-card" *ngIf="config$ | async as config">
      <h3>⚙️ Configuration du module</h3>
      <div class="config-grid">
        <div><span>Source API</span><b>{{ config.apiSource | uppercase }}</b></div>
        <div><span>Fréquence</span><b>{{ config.syncFrequency }}</b></div>
        <div><span>TVA par défaut</span><b>{{ config.defaultVat }}%</b></div>
        <div><span>Langue</span><b>{{ config.defaultLanguage | uppercase }}</b></div>
      </div>
    </div>

    <div class="sync-actions">
      <div class="action-card">
        <div class="action-icon">🔍</div>
        <h3>Sync par OEM</h3>
        <p>Synchronise une pièce spécifique via son numéro OEM</p>
        <input type="text" [(ngModel)]="oemSync" placeholder="Numéro OEM" class="sync-input" />
        <button (click)="syncOem()" [disabled]="syncing || !oemSync" class="btn-sync">
          <span *ngIf="!syncing">Lancer</span>
          <span *ngIf="syncing">⏳ Sync en cours...</span>
        </button>
      </div>

      <div class="action-card">
        <div class="action-icon">🚗</div>
        <h3>Sync par Véhicule</h3>
        <p>Synchronise toutes les pièces compatibles avec un véhicule</p>
        <input type="number" [(ngModel)]="vehicleId" placeholder="ID Véhicule (TecDoc)" class="sync-input" />
        <button (click)="syncVehicle()" [disabled]="syncing || !vehicleId" class="btn-sync">
          <span *ngIf="!syncing">Lancer</span>
          <span *ngIf="syncing">⏳ Sync en cours...</span>
        </button>
      </div>

      <div class="action-card">
        <div class="action-icon">📦</div>
        <h3>Sync Complète</h3>
        <p>Synchronisation complète du catalogue (long)</p>
        <input type="number" [(ngModel)]="maxPages" placeholder="Nombre max de pages" class="sync-input" [value]="5" />
        <button (click)="syncFull()" [disabled]="syncing" class="btn-sync warning">
          <span *ngIf="!syncing">⚠️ Lancer sync complète</span>
          <span *ngIf="syncing">⏳ Sync en cours...</span>
        </button>
      </div>
    </div>

    <div class="result-panel" *ngIf="lastResult">
      <h3>📊 Résultat du dernier sync</h3>
      <div class="stats-grid">
        <div class="stat success">
          <span class="stat-value">{{ lastResult.productsCreated }}</span>
          <span class="stat-label">Créés</span>
        </div>
        <div class="stat info">
          <span class="stat-value">{{ lastResult.productsUpdated }}</span>
          <span class="stat-label">Mis à jour</span>
        </div>
        <div class="stat info">
          <span class="stat-value">{{ lastResult.imagesAdded }}</span>
          <span class="stat-label">Images</span>
        </div>
        <div class="stat info">
          <span class="stat-value">{{ lastResult.vehiclesAdded }}</span>
          <span class="stat-label">Véhicules</span>
        </div>
        <div class="stat" [class.error]="lastResult.errorsCount > 0">
          <span class="stat-value">{{ lastResult.errorsCount }}</span>
          <span class="stat-label">Erreurs</span>
        </div>
      </div>
      <div class="job-id">Job ID: <code>{{ lastResult.jobId }}</code></div>
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 24px; }
    .page-header h1 { font-size: 24px; color: #1a1a2e; margin: 0; }
    .subtitle { color: #888; margin-top: 4px; }
    .config-card { background: #fff; border-radius: 12px; padding: 20px; margin-bottom: 24px; box-shadow: 0 2px 8px rgba(0,0,0,0.06); }
    .config-card h3 { margin: 0 0 16px; font-size: 14px; color: #666; text-transform: uppercase; }
    .config-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 16px; }
    .config-grid div { display: flex; flex-direction: column; gap: 4px; }
    .config-grid span { font-size: 12px; color: #888; }
    .config-grid b { font-size: 14px; color: #1a1a2e; }
    .sync-actions { display: grid; grid-template-columns: repeat(3, 1fr); gap: 20px; margin-bottom: 24px; }
    .action-card { background: #fff; border-radius: 12px; padding: 24px; text-align: center; box-shadow: 0 2px 8px rgba(0,0,0,0.06); }
    .action-icon { font-size: 32px; margin-bottom: 12px; }
    .action-card h3 { margin: 0 0 8px; font-size: 16px; color: #1a1a2e; }
    .action-card p { font-size: 13px; color: #888; margin-bottom: 16px; min-height: 36px; }
    .sync-input { width: 100%; padding: 10px; border: 1px solid #ddd; border-radius: 8px; margin-bottom: 12px; font-size: 14px; box-sizing: border-box; }
    .btn-sync { width: 100%; background: #e94560; color: #fff; border: none; padding: 10px; border-radius: 8px; font-weight: 600; cursor: pointer; }
    .btn-sync:hover:not(:disabled) { opacity: 0.9; }
    .btn-sync:disabled { opacity: 0.5; cursor: not-allowed; }
    .btn-sync.warning { background: #ff9800; }
    .result-panel { background: #fff; border-radius: 12px; padding: 24px; box-shadow: 0 2px 8px rgba(0,0,0,0.06); }
    .result-panel h3 { margin: 0 0 16px; font-size: 14px; color: #666; }
    .stats-grid { display: grid; grid-template-columns: repeat(5, 1fr); gap: 16px; margin-bottom: 16px; }
    .stat { background: #f8f9fa; border-radius: 10px; padding: 16px; text-align: center; }
    .stat.success { background: #e8f5e9; color: #2e7d32; }
    .stat.info { background: #e3f2fd; color: #1565c0; }
    .stat.error { background: #ffebee; color: #c62828; }
    .stat-value { display: block; font-size: 24px; font-weight: 700; }
    .stat-label { font-size: 12px; text-transform: uppercase; opacity: 0.8; }
    .job-id { font-size: 12px; color: #888; }
    .job-id code { background: #f0f0f0; padding: 2px 6px; border-radius: 4px; }
  `]
})
export class SyncPanelComponent implements OnInit {
  config$!: Observable<AutoPartsModuleConfig | null>;
  oemSync = '';
  vehicleId?: number;
  maxPages = 5;
  syncing = false;
  lastResult?: SyncResult;

  constructor(
    private autoPartsService: AutoPartsService,
    private moduleService: ModuleService
  ) {}

  ngOnInit(): void {
    this.config$ = this.moduleService.getAutoPartsConfig();
  }

  syncOem(): void {
    if (!this.oemSync) return;
    this.runSync({ syncType: 'oem', oemNumber: this.oemSync });
  }

  syncVehicle(): void {
    if (!this.vehicleId) return;
    this.runSync({ syncType: 'vehicle', vehicleId: this.vehicleId });
  }

  syncFull(): void {
    this.runSync({ syncType: 'full', maxPages: this.maxPages });
  }

  private runSync(request: any): void {
    this.syncing = true;
    this.autoPartsService.syncCatalog(request).subscribe({
      next: result => this.lastResult = result,
      error: err => alert('Erreur sync: ' + err.message),
      complete: () => this.syncing = false
    });
  }
}
