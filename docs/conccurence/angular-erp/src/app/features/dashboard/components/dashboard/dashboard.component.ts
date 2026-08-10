// ============================================================
// src/app/features/dashboard/components/dashboard/dashboard.component.ts
// Tableau de bord avec stats et alertes
// ============================================================

import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Observable, combineLatest } from 'rxjs';
import { map } from 'rxjs/operators';
import { ModuleService } from '../../../../core/services/module.service';
import { AuthService } from '../../../../core/services/auth.service';

interface DashboardStats {
  totalProducts: number;
  lowStock: number;
  lastSync: string;
  activeModules: string[];
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="dashboard">
      <div class="page-header">
        <h1>📊 Tableau de bord</h1>
        <p *ngIf="user$ | async as user">Bienvenue, <b>{{ user.name }}</b> — {{ user.companyId }}</p>
      </div>

      <div class="stats-grid">
        <div class="stat-card primary">
          <div class="stat-icon">📦</div>
          <div class="stat-value">12,450</div>
          <div class="stat-label">Produits en base</div>
        </div>
        <div class="stat-card warning">
          <div class="stat-icon">⚠️</div>
          <div class="stat-value">23</div>
          <div class="stat-label">Stock critique</div>
        </div>
        <div class="stat-card success">
          <div class="stat-icon">🔄</div>
          <div class="stat-value">02:15</div>
          <div class="stat-label">Dernier sync (cette nuit)</div>
        </div>
        <div class="stat-card info">
          <div class="stat-icon">🖼️</div>
          <div class="stat-value">8,320</div>
          <div class="stat-label">Images synchronisées</div>
        </div>
      </div>

      <!-- Section Pièces Auto (visible uniquement si module actif) -->
      <div *requireModule="'auto_parts'" class="module-panel">
        <h2>🚗 Module Pièces Auto</h2>
        <div class="panel-content">
          <div class="quick-actions">
            <a routerLink="/auto-parts/oem" class="action-tile">
              <span class="tile-icon">🔍</span>
              <span class="tile-label">Recherche OEM</span>
            </a>
            <a routerLink="/auto-parts/vehicle" class="action-tile">
              <span class="tile-icon">🚗</span>
              <span class="tile-label">Par véhicule</span>
            </a>
            <a routerLink="/auto-parts/sync" class="action-tile">
              <span class="tile-icon">🔄</span>
              <span class="tile-label">Sync catalogue</span>
            </a>
          </div>
          <div class="sync-status">
            <div class="status-row">
              <span>Source API</span>
              <b>{{ (config$ | async)?.apiSource | uppercase }}</b>
            </div>
            <div class="status-row">
              <span>Fréquence</span>
              <b>{{ (config$ | async)?.syncFrequency }}</b>
            </div>
            <div class="status-row">
              <span>Prochain sync</span>
              <b>Cette nuit à 02:00</b>
            </div>
          </div>
        </div>
      </div>

      <!-- Section Quincaillerie -->
      <div *requireModule="'hardware'" class="module-panel">
        <h2>🔩 Module Quincaillerie</h2>
        <p>Attributs configurés : filetage, matériau, norme DIN</p>
      </div>

      <!-- Alertes stock -->
      <div class="alerts-panel">
        <h2>⚠️ Alertes stock</h2>
        <div class="alert-list">
          <div class="alert-item" *ngFor="let alert of stockAlerts">
            <span class="alert-ref">{{ alert.ref }}</span>
            <span class="alert-name">{{ alert.name }}</span>
            <span class="alert-qty" [class.critical]="alert.qty === 0">
              {{ alert.qty === 0 ? 'RUPTURE' : alert.qty + ' restant(s)' }}
            </span>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .dashboard { max-width: 1200px; }
    .page-header { margin-bottom: 24px; }
    .page-header h1 { font-size: 24px; color: #1a1a2e; margin: 0; }
    .page-header p { color: #888; margin-top: 4px; }
    .stats-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 20px; margin-bottom: 24px; }
    .stat-card { background: #fff; border-radius: 16px; padding: 24px; box-shadow: 0 2px 8px rgba(0,0,0,0.06); text-align: center; }
    .stat-card.primary { border-top: 4px solid #667eea; }
    .stat-card.warning { border-top: 4px solid #ff9800; }
    .stat-card.success { border-top: 4px solid #2e7d32; }
    .stat-card.info { border-top: 4px solid #0288d1; }
    .stat-icon { font-size: 28px; margin-bottom: 8px; }
    .stat-value { font-size: 28px; font-weight: 700; color: #1a1a2e; }
    .stat-label { font-size: 12px; color: #888; text-transform: uppercase; margin-top: 4px; }
    .module-panel { background: #fff; border-radius: 16px; padding: 24px; margin-bottom: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.06); }
    .module-panel h2 { font-size: 16px; color: #1a1a2e; margin: 0 0 16px; }
    .panel-content { display: flex; gap: 24px; }
    .quick-actions { display: flex; gap: 12px; flex: 1; }
    .action-tile {
      flex: 1; background: #f8f9fa; border-radius: 12px; padding: 20px;
      text-align: center; text-decoration: none; color: #1a1a2e;
      transition: all 0.2s; cursor: pointer;
    }
    .action-tile:hover { background: #e94560; color: #fff; transform: translateY(-2px); }
    .tile-icon { display: block; font-size: 28px; margin-bottom: 8px; }
    .tile-label { font-size: 13px; font-weight: 600; }
    .sync-status { width: 280px; background: #f8f9fa; border-radius: 12px; padding: 16px; }
    .status-row { display: flex; justify-content: space-between; padding: 8px 0; border-bottom: 1px solid #eee; font-size: 13px; }
    .status-row:last-child { border-bottom: none; }
    .status-row span { color: #888; }
    .status-row b { color: #1a1a2e; }
    .alerts-panel { background: #fff; border-radius: 16px; padding: 24px; box-shadow: 0 2px 8px rgba(0,0,0,0.06); }
    .alerts-panel h2 { font-size: 16px; color: #1a1a2e; margin: 0 0 16px; }
    .alert-list { display: flex; flex-direction: column; gap: 8px; }
    .alert-item { display: flex; align-items: center; gap: 16px; padding: 10px 16px; background: #fafafa; border-radius: 8px; font-size: 13px; }
    .alert-ref { font-family: monospace; color: #667eea; font-weight: 600; min-width: 120px; }
    .alert-name { flex: 1; color: #444; }
    .alert-qty { color: #ff9800; font-weight: 600; font-size: 12px; }
    .alert-qty.critical { color: #e94560; }
  `]
})
export class DashboardComponent implements OnInit {
  user$ = this.authService.getUser();
  config$ = this.moduleService.getAutoPartsConfig();

  stockAlerts = [
    { ref: '0281002937', name: 'Capteur de pression Bosch', qty: 2 },
    { ref: '7700105767', name: 'Filtre à huile Renault', qty: 0 },
    { ref: '0986280411', name: 'Débitmètre d'air Bosch', qty: 3 },
    { ref: '8200435691', name: 'Capteur PMH Renault', qty: 1 },
  ];

  constructor(
    private authService: AuthService,
    private moduleService: ModuleService
  ) {}

  ngOnInit(): void {
    const user = this.authService.getCurrentUser();
    if (user) {
      this.moduleService.loadModules(user.companyId).subscribe();
    }
  }
}
