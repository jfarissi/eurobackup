import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { TPipe } from '../../../pipes/t.pipe';

@Component({
  selector: 'app-catalog-subnav',
  standalone: true,
  imports: [CommonModule, RouterModule, TPipe],
  template: `
    <nav class="catalog-subnav" aria-label="Catalogue">
      <a routerLink="/erp-products" routerLinkActive="active" [routerLinkActiveOptions]="{exact:true}">{{ 'catalog.nav.products' | t }}</a>
      <a routerLink="/plate-scan" routerLinkActive="active">{{ 'catalog.nav.plateScan' | t }}</a>
      <a routerLink="/erp-brands" routerLinkActive="active">{{ 'catalog.nav.brands' | t }}</a>
      <a routerLink="/erp-categories" routerLinkActive="active">{{ 'catalog.nav.categories' | t }}</a>
    </nav>
  `,
  styles: [`
    .catalog-subnav {
      display: flex;
      gap: 4px;
      flex-wrap: wrap;
      margin-top: 12px;
    }
    .catalog-subnav a {
      padding: 6px 12px;
      border-radius: 6px;
      font-size: 13px;
      font-weight: 500;
      color: var(--on-surface-variant);
      text-decoration: none;
    }
    .catalog-subnav a:hover { background: rgba(0,0,0,0.04); }
    .catalog-subnav a.active {
      background: var(--primary-container, #e8def8);
      color: var(--on-primary-container, #1d192b);
    }
  `]
})
export class CatalogSubnavComponent {}
