import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { MaterialModule } from '../../material.module';
import { AuthService } from '../../services/auth.service';
import { PermissionService } from '../../services/permission.service';
import { AppI18nService, AppLang } from '../../services/app-i18n.service';
import { TPipe } from '../../pipes/t.pipe';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, MaterialModule, TPipe],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css']
})
export class LoginComponent {
  username = '';
  password = '';
  loading = false;
  error = '';
  hidePassword = true;

  constructor(
    private auth: AuthService,
    private router: Router,
    private perm: PermissionService,
    public i18n: AppI18nService
  ) {
    if (this.auth.isLoggedIn) {
      void this.router.navigateByUrl(this.perm.getDefaultHomeUrl());
    }
  }

  setLanguage(lang: AppLang): void {
    this.i18n.setLang(lang);
  }

  submit(): void {
    this.error = '';
    if (!this.username.trim() || !this.password) {
      this.error = this.i18n.t('login.required');
      return;
    }

    this.loading = true;
    this.auth.login({
      username: this.username.trim(),
      password: this.password
    }).subscribe({
      next: () => {
        this.loading = false;
        void this.router.navigateByUrl(this.perm.getDefaultHomeUrl());
      },
      error: (err) => {
        this.loading = false;
        this.error = err?.error?.message || this.i18n.t('login.invalid');
      }
    });
  }
}
