// ============================================================
// src/app/shared/components/module-badge/module-badge.component.ts
// Affiche un badge coloré selon le module actif
// ============================================================

import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-module-badge',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span class="badge" [class.active]="isActive" [class.inactive]="!isActive">
      {{ isActive ? '✓ ' + label : '✕ ' + label }}
    </span>
  `,
  styles: [`
    .badge {
      display: inline-block;
      padding: 4px 10px;
      border-radius: 12px;
      font-size: 11px;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }
    .active {
      background: #e8f5e9;
      color: #2e7d32;
      border: 1px solid #81c784;
    }
    .inactive {
      background: #fafafa;
      color: #9e9e9e;
      border: 1px solid #e0e0e0;
    }
  `]
})
export class ModuleBadgeComponent {
  @Input() label = '';
  @Input() isActive = false;
}
