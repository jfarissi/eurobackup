import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { MaterialModule } from '../../material.module';
import { AuthService } from '../../services/auth.service';
import { PermissionService } from '../../services/permission.service';
import { TPipe } from '../../pipes/t.pipe';

@Component({
  selector: 'app-access-denied',
  standalone: true,
  imports: [CommonModule, RouterModule, MaterialModule, TPipe],
  template: `
    <div class="wrap">
      <mat-icon>lock</mat-icon>
      <h2>{{ 'accessDenied.title' | t }}</h2>
      <p>{{ 'accessDenied.message' | t }}</p>
      <p class="muted" *ngIf="userLabel">{{ 'accessDenied.asUser' | t }} <strong>{{ userLabel }}</strong>
        <span *ngIf="roleLabel"> ({{ roleLabel }})</span>
      </p>
      <div class="actions">
        <button class="btn-primary" type="button" (click)="goHome()" *ngIf="hasAnyHome">{{ 'accessDenied.goHome' | t }}</button>
        <button class="btn-secondary" type="button" (click)="logout()">{{ 'accessDenied.logout' | t }}</button>
      </div>
    </div>
  `,
  styles: [`
    .wrap { max-width: 480px; margin: 80px auto; text-align: center; padding: 24px; }
    mat-icon { font-size: 48px; width: 48px; height: 48px; color: #c62828; }
    h2 { margin: 16px 0 8px; }
    p { color: #555; }
    .muted { color: #888; font-size: 0.9rem; }
    .actions { display: flex; gap: 12px; justify-content: center; margin-top: 24px; flex-wrap: wrap; }
    .btn-primary { background: #37474f; color: #fff; border: none; padding: 10px 18px; border-radius: 6px; cursor: pointer; }
    .btn-secondary { background: #fff; border: 1px solid #ccc; padding: 10px 18px; border-radius: 6px; cursor: pointer; }
  `]
})
export class AccessDeniedComponent {
  constructor(
    private auth: AuthService,
    private perm: PermissionService,
    private router: Router
  ) {}

  get userLabel(): string {
    const u = this.auth.currentUser;
    if (!u) return '';
    return [u.firstName, u.lastName].filter(Boolean).join(' ') || u.username;
  }

  get roleLabel(): string {
    return this.auth.currentUser?.role || '';
  }

  get hasAnyHome(): boolean {
    return this.perm.getDefaultHomeUrl('/access-denied') !== '/access-denied';
  }

  goHome(): void {
    void this.router.navigateByUrl(this.perm.getDefaultHomeUrl('/access-denied'));
  }

  logout(): void {
    this.auth.logout();
    void this.router.navigate(['/login']);
  }
}
