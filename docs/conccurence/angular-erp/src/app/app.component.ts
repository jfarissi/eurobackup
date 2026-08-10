// ============================================================
// src/app/app.component.ts
// ============================================================

import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { NavMenuComponent } from './shared/components/nav-menu/nav-menu.component';
import { ModuleService } from './core/services/module.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterModule, NavMenuComponent],
  template: `
    <div class="app-layout">
      <app-nav-menu class="sidebar"></app-nav-menu>
      <main class="content">
        <router-outlet></router-outlet>
      </main>
    </div>
  `,
  styles: [`
    .app-layout { display: flex; min-height: 100vh; background: #f5f7fa; }
    .sidebar { position: fixed; left: 0; top: 0; bottom: 0; z-index: 100; }
    .content { flex: 1; margin-left: 260px; padding: 24px 32px; min-height: 100vh; }
  `]
})
export class AppComponent implements OnInit {
  constructor(private moduleService: ModuleService) {}

  ngOnInit(): void {
    // Charge les modules de la société au démarrage
    // En prod, le CompanyId vient du JWT / auth service
    const companyId = localStorage.getItem('company_id') || 'COMP-001';
    this.moduleService.loadModules(companyId).subscribe();
  }
}
