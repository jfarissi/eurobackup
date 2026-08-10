// ============================================================
// src/app/shared/components/nav-menu/nav-menu.component.ts
// v2 — Ajout du lien "Scan Plaque" dans le menu Pièces Auto
// ============================================================

import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Observable } from 'rxjs';
import { ModuleService } from '../../../core/services/module.service';

interface NavItem {
  label: string;
  route: string;
  icon: string;
  moduleCode?: string;
}

@Component({
  selector: 'app-nav-menu',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <nav class="nav-sidebar">
      <div class="logo">
        <span class="logo-icon">⚙️</span>
        <span class="logo-text">MyERP</span>
      </div>

      <div class="nav-section">
        <span class="section-title">Général</span>
        <a *ngFor="let item of coreNavItems" [routerLink]="item.route"
           routerLinkActive="active" class="nav-item">
          <span class="nav-icon">{{ item.icon }}</span>
          <span class="nav-label">{{ item.label }}</span>
        </a>
      </div>

      <!-- Module Pièces Auto -->
      <div class="nav-section" *ngIf="hasAutoParts$ | async">
        <span class="section-title">🚗 Pièces Auto</span>
        <a *ngFor="let item of autoPartsNavItems" [routerLink]="item.route"
           routerLinkActive="active" class="nav-item">
          <span class="nav-icon">{{ item.icon }}</span>
          <span class="nav-label">{{ item.label }}</span>
        </a>
      </div>

      <div class="nav-footer">
        <span class="badge" [class.on]="hasAutoParts$ | async">🚗 Auto</span>
        <span class="badge" [class.on]="hasHardware$ | async">🔩 Quinc.</span>
      </div>
    </nav>
  `,
  styles: [`
    .nav-sidebar {
      width: 260px;
      height: 100vh;
      background: #1a1a2e;
      color: #fff;
      display: flex;
      flex-direction: column;
      padding: 20px 0;
    }
    .logo {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 0 20px 24px;
      border-bottom: 1px solid rgba(255,255,255,0.1);
      margin-bottom: 16px;
    }
    .logo-icon { font-size: 24px; }
    .logo-text { font-size: 20px; font-weight: 700; }
    .nav-section { margin-bottom: 8px; }
    .section-title {
      display: block;
      padding: 12px 20px 6px;
      font-size: 10px;
      text-transform: uppercase;
      letter-spacing: 1px;
      color: rgba(255,255,255,0.4);
    }
    .nav-item {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 10px 20px;
      color: rgba(255,255,255,0.7);
      text-decoration: none;
      transition: all 0.2s;
      cursor: pointer;
    }
    .nav-item:hover, .nav-item.active {
      background: rgba(233,69,96,0.15);
      color: #e94560;
      border-left: 3px solid #e94560;
    }
    .nav-icon { font-size: 16px; width: 20px; text-align: center; }
    .nav-label { font-size: 13px; }
    .nav-footer {
      margin-top: auto;
      padding: 16px 20px;
      border-top: 1px solid rgba(255,255,255,0.1);
      display: flex;
      gap: 8px;
      flex-wrap: wrap;
    }
    .badge {
      font-size: 10px;
      padding: 4px 8px;
      border-radius: 12px;
      background: rgba(255,255,255,0.1);
      color: rgba(255,255,255,0.5);
    }
    .badge.on {
      background: rgba(46,125,50,0.3);
      color: #81c784;
    }
  `]
})
export class NavMenuComponent implements OnInit {
  hasAutoParts$!: Observable<boolean>;
  hasHardware$!: Observable<boolean>;

  coreNavItems: NavItem[] = [
    { label: 'Tableau de bord', route: '/dashboard', icon: '📊' },
    { label: 'Produits', route: '/products', icon: '📦' },
    { label: 'Clients', route: '/clients', icon: '👥' },
    { label: 'Factures', route: '/invoices', icon: '📄' },
  ];

  autoPartsNavItems: NavItem[] = [
    { label: 'Scan Plaque', route: '/auto-parts/plate', icon: '📸' },
    { label: 'Recherche OEM', route: '/auto-parts/oem', icon: '🔍' },
    { label: 'Par Véhicule', route: '/auto-parts/vehicle', icon: '🚗' },
    { label: 'Sync Catalogue', route: '/auto-parts/sync', icon: '🔄' },
  ];

  constructor(private moduleService: ModuleService) {}

  ngOnInit(): void {
    this.hasAutoParts$ = this.moduleService.hasModule('auto_parts');
    this.hasHardware$ = this.moduleService.hasModule('hardware');
  }
}
