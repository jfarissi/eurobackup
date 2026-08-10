// ============================================================
// src/app/core/services/auth.service.ts
// Gestion de l'authentification et du token JWT
// ============================================================

import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, of } from 'rxjs';
import { tap, catchError } from 'rxjs/operators';

export interface User {
  id: string;
  email: string;
  name: string;
  companyId: string;
  role: string;
}

export interface LoginResponse {
  token: string;
  user: User;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly apiUrl = '/api/auth';
  private user$ = new BehaviorSubject<User | null>(null);
  private token: string | null = null;

  constructor(private http: HttpClient) {
    // Restore session from localStorage
    const saved = localStorage.getItem('erp_auth');
    if (saved) {
      try {
        const data = JSON.parse(saved);
        this.token = data.token;
        this.user$.next(data.user);
      } catch { /* ignore */ }
    }
  }

  getToken(): string | null {
    return this.token;
  }

  getUser(): Observable<User | null> {
    return this.user$.asObservable();
  }

  getCurrentUser(): User | null {
    return this.user$.value;
  }

  login(email: string, password: string, companyId: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.apiUrl}/login`, { email, password, companyId }).pipe(
      tap(res => {
        this.token = res.token;
        this.user$.next(res.user);
        localStorage.setItem('erp_auth', JSON.stringify(res));
        localStorage.setItem('company_id', res.user.companyId);
      })
    );
  }

  logout(): void {
    this.token = null;
    this.user$.next(null);
    localStorage.removeItem('erp_auth');
    localStorage.removeItem('company_id');
  }

  isAuthenticated(): boolean {
    return !!this.token;
  }
}
