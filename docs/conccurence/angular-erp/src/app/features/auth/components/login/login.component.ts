// ============================================================
// src/app/features/auth/components/login/login.component.ts
// ============================================================

import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../../../core/services/auth.service';
import { ModuleService } from '../../../../core/services/module.service';

interface Company {
  id: string;
  name: string;
}

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="login-page">
      <div class="login-card">
        <div class="logo">
          <span class="logo-icon">⚙️</span>
          <h1>MyERP</h1>
          <p>Gestion modulaire pour tous vos clients</p>
        </div>

        <div class="form-group">
          <label>Société</label>
          <select [(ngModel)]="selectedCompany" class="form-input">
            <option value="">Choisir une société...</option>
            <option *ngFor="let c of companies" [value]="c.id">{{ c.name }}</option>
          </select>
        </div>

        <div class="form-group">
          <label>Email</label>
          <input type="email" [(ngModel)]="email" placeholder="admin@example.com" class="form-input" />
        </div>

        <div class="form-group">
          <label>Mot de passe</label>
          <input type="password" [(ngModel)]="password" placeholder="••••••••" class="form-input" />
        </div>

        <button (click)="login()" [disabled]="loading || !selectedCompany" class="btn-login">
          <span *ngIf="!loading">Se connecter</span>
          <span *ngIf="loading">Connexion...</span>
        </button>

        <div class="error" *ngIf="error">{{ error }}</div>

        <div class="demo-companies">
          <p>🔧 Démo :</p>
          <span class="demo-tag" *ngFor="let c of companies" (click)="selectedCompany = c.id">
            {{ c.id }}
          </span>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .login-page {
      min-height: 100vh; display: flex; align-items: center; justify-content: center;
      background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
    }
    .login-card {
      background: #fff; border-radius: 20px; padding: 40px; width: 100%; max-width: 400px;
      box-shadow: 0 20px 60px rgba(0,0,0,0.3);
    }
    .logo { text-align: center; margin-bottom: 32px; }
    .logo-icon { font-size: 48px; }
    .logo h1 { margin: 8px 0 4px; color: #1a1a2e; font-size: 28px; }
    .logo p { color: #888; font-size: 14px; margin: 0; }
    .form-group { margin-bottom: 16px; }
    label { display: block; font-size: 12px; font-weight: 600; color: #666; text-transform: uppercase; margin-bottom: 6px; }
    .form-input {
      width: 100%; padding: 12px 16px; border: 1px solid #ddd; border-radius: 10px;
      font-size: 14px; outline: none; box-sizing: border-box;
    }
    .form-input:focus { border-color: #e94560; }
    .btn-login {
      width: 100%; background: #e94560; color: #fff; border: none;
      padding: 14px; border-radius: 10px; font-size: 15px; font-weight: 600;
      cursor: pointer; transition: opacity 0.2s; margin-top: 8px;
    }
    .btn-login:hover:not(:disabled) { opacity: 0.9; }
    .btn-login:disabled { opacity: 0.5; cursor: not-allowed; }
    .error { color: #e94560; font-size: 13px; margin-top: 12px; text-align: center; }
    .demo-companies { margin-top: 24px; padding-top: 16px; border-top: 1px solid #eee; }
    .demo-companies p { font-size: 12px; color: #888; margin: 0 0 8px; }
    .demo-tag {
      display: inline-block; background: #f0f0f0; color: #666; padding: 4px 10px;
      border-radius: 6px; font-size: 11px; font-family: monospace; margin-right: 6px;
      cursor: pointer; transition: all 0.2s;
    }
    .demo-tag:hover { background: #e94560; color: #fff; }
  `]
})
export class LoginComponent {
  email = 'admin@example.com';
  password = 'password';
  selectedCompany = '';
  loading = false;
  error = '';

  companies: Company[] = [
    { id: 'COMP-001', name: 'Garage Auto Dupont (Pièces Auto)' },
    { id: 'COMP-002', name: 'Quincaillerie Martin' },
    { id: 'COMP-003', name: 'Electro Plus' },
  ];

  constructor(
    private authService: AuthService,
    private moduleService: ModuleService,
    private router: Router
  ) {}

  login(): void {
    if (!this.selectedCompany) return;
    this.loading = true;
    this.error = '';

    this.authService.login(this.email, this.password, this.selectedCompany).subscribe({
      next: () => {
        // Charge les modules de la société
        this.moduleService.loadModules(this.selectedCompany).subscribe(() => {
          this.router.navigate(['/products']);
        });
      },
      error: err => {
        this.error = err.error?.message || 'Erreur de connexion';
        this.loading = false;
      }
    });
  }
}
