import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MaterialModule } from '../../material.module';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, MaterialModule],
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
    private route: ActivatedRoute
  ) {
    if (this.auth.isLoggedIn) {
      void this.router.navigateByUrl(this.returnUrl);
    }
  }

  get returnUrl(): string {
    return this.route.snapshot.queryParamMap.get('returnUrl') || '/upload';
  }

  submit(): void {
    this.error = '';
    if (!this.username.trim() || !this.password) {
      this.error = 'Email et mot de passe requis';
      return;
    }

    this.loading = true;
    this.auth.login({
      username: this.username.trim(),
      password: this.password
    }).subscribe({
      next: () => {
        this.loading = false;
        void this.router.navigateByUrl(this.returnUrl);
      },
      error: (err) => {
        this.loading = false;
        this.error = err?.error?.message || 'Identifiants incorrects';
      }
    });
  }
}
