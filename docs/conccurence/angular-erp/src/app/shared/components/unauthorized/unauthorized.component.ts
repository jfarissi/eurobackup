// ============================================================
// src/app/shared/components/unauthorized/unauthorized.component.ts
// ============================================================

import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-unauthorized',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="unauthorized">
      <div class="icon">🚫</div>
      <h1>Accès refusé</h1>
      <p>Ce module n'est pas activé pour votre société.</p>
      <p class="sub">Contactez votre administrateur pour l'activer.</p>
      <a routerLink="/products" class="btn-back">← Retour au catalogue</a>
    </div>
  `,
  styles: [`
    .unauthorized { text-align: center; padding: 80px 20px; }
    .icon { font-size: 64px; margin-bottom: 16px; }
    h1 { color: #1a1a2e; margin: 0 0 8px; }
    p { color: #666; font-size: 16px; margin: 0; }
    .sub { color: #888; font-size: 14px; margin-top: 8px; }
    .btn-back { display: inline-block; margin-top: 24px; color: #e94560; text-decoration: none; font-weight: 600; }
    .btn-back:hover { text-decoration: underline; }
  `]
})
export class UnauthorizedComponent {}
