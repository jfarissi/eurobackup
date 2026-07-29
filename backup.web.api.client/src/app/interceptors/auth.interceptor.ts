import { Injectable, Injector, inject } from '@angular/core';
import {
  HttpErrorResponse,
  HttpEvent,
  HttpHandler,
  HttpInterceptor,
  HttpRequest
} from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { CompanyService } from '../services/company.service';

/**
 * Injection lazy via Injector pour éviter NG0200 :
 * HTTP_INTERCEPTORS → Auth/Company → HttpClient → HTTP_INTERCEPTORS
 */
@Injectable()
export class AuthInterceptor implements HttpInterceptor {
  private readonly injector = inject(Injector);
  private readonly router = inject(Router);

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    const auth = this.injector.get(AuthService);
    const companyService = this.injector.get(CompanyService);

    const headers: Record<string, string> = {};
    const token = auth.token;
    if (token) headers['Authorization'] = `Bearer ${token}`;
    const companyId = companyService.activeCompanyId;
    if (companyId) headers['X-Company-ID'] = companyId;

    const authReq = Object.keys(headers).length
      ? req.clone({ setHeaders: headers })
      : req;

    return next.handle(authReq).pipe(
      catchError((err: HttpErrorResponse) => {
        if (err.status === 401 && !req.url.includes('/auth/login')) {
          auth.logout();
          void this.router.navigate(['/login']);
        }
        return throwError(() => err);
      })
    );
  }
}
