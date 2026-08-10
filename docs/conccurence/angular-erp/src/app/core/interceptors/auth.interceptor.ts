// ============================================================
// src/app/core/interceptors/auth.interceptor.ts
// Ajoute le Bearer token + X-Company-Id à chaque requête
// ============================================================

import { Injectable } from '@angular/core';
import { HttpInterceptor, HttpRequest, HttpHandler, HttpEvent } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthService } from '../services/auth.service';

@Injectable()
export class AuthInterceptor implements HttpInterceptor {
  constructor(private authService: AuthService) {}

  intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    const token = this.authService.getToken();
    const user = this.authService.getCurrentUser();

    let headers = req.headers;
    if (token) {
      headers = headers.set('Authorization', `Bearer ${token}`);
    }
    if (user) {
      headers = headers.set('X-Company-Id', user.companyId);
    }

    const cloned = req.clone({ headers });
    return next.handle(cloned);
  }
}
