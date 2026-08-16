import { Injectable, NgZone, OnDestroy } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, Subscription, map, tap, catchError, of, filter } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthUser, LoginRequest, LoginResponse } from '../models/auth';
import { CompanyService } from './company.service';

const TOKEN_KEY = 'backup_auth_token';
const USER_KEY = 'backup_auth_user';
/** Rafraîchit les permissions en secours si SignalR est indisponible. */
const PERMISSION_REFRESH_MS = 5 * 60_000;

@Injectable({ providedIn: 'root' })
export class AuthService implements OnDestroy {
  private readonly baseUrl = `${environment.apiBaseUrl}/auth`;
  private readonly userSubject = new BehaviorSubject<AuthUser | null>(this.readStoredUser());
  private refreshSub: Subscription | null = null;
  private refreshTimer: ReturnType<typeof setInterval> | null = null;
  private refreshing = false;
  private lastRefreshAt = 0;
  private readonly onVisibility = () => {
    if (document.visibilityState === 'visible' && this.isLoggedIn) {
      this.refreshSession();
    }
  };

  readonly user$ = this.userSubject.asObservable();

  constructor(
    private http: HttpClient,
    private companyService: CompanyService,
    private zone: NgZone
  ) {
    if (typeof document !== 'undefined') {
      document.addEventListener('visibilitychange', this.onVisibility);
    }
    // Différer après la construction DI (évite cycle HttpClient / interceptors)
    if (this.isLoggedIn) {
      queueMicrotask(() => {
        this.startPermissionSync();
        this.refreshSession();
      });
    }
  }

  ngOnDestroy(): void {
    this.stopPermissionSync();
    if (typeof document !== 'undefined') {
      document.removeEventListener('visibilitychange', this.onVisibility);
    }
  }

  get token(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  get currentUser(): AuthUser | null {
    return this.userSubject.value;
  }

  get isLoggedIn(): boolean {
    return !!this.token;
  }

  login(request: LoginRequest): Observable<AuthUser> {
    return this.http.post<LoginResponse>(`${this.baseUrl}/login`, request).pipe(
      tap((res) => {
        this.persistSession(res);
        this.startPermissionSync();
      }),
      map((res) => this.toUser(res))
    );
  }

  /** Recharge permissions (+ JWT) depuis le serveur — sans se déconnecter. */
  me(): Observable<AuthUser> {
    return this.http.get<LoginResponse | AuthUser>(`${this.baseUrl}/me`).pipe(
      tap((res) => this.applyMeResponse(res)),
      map((res) => this.toUser(res))
    );
  }

  /** Déclenche un refresh silencieux (ignore erreurs réseau). */
  refreshSession(force = false): void {
    if (!this.isLoggedIn || this.refreshing) return;
    const now = Date.now();
    if (!force && now - this.lastRefreshAt < 5_000) return;
    this.refreshing = true;
    this.lastRefreshAt = now;
    this.refreshSub?.unsubscribe();
    this.refreshSub = this.me().pipe(
      catchError(() => of(null)),
      tap(() => { this.refreshing = false; }),
      filter((u): u is AuthUser => !!u)
    ).subscribe();
  }

  switchCompany(companyId: string): Observable<AuthUser> {
    return this.http.post<LoginResponse>(`${this.baseUrl}/switch-company`, { companyId }).pipe(
      tap((res) => this.persistSession(res)),
      map((res) => this.toUser(res))
    );
  }

  logout(): void {
    this.stopPermissionSync();
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this.companyService.setActiveCompanyId(null);
    this.userSubject.next(null);
  }

  private startPermissionSync(): void {
    this.stopPermissionSync();
    this.zone.runOutsideAngular(() => {
      this.refreshTimer = setInterval(() => {
        this.zone.run(() => this.refreshSession());
      }, PERMISSION_REFRESH_MS);
    });
  }

  private stopPermissionSync(): void {
    if (this.refreshTimer != null) {
      clearInterval(this.refreshTimer);
      this.refreshTimer = null;
    }
    this.refreshSub?.unsubscribe();
    this.refreshSub = null;
    this.refreshing = false;
  }

  private applyMeResponse(res: LoginResponse | AuthUser): void {
    const token = (res as LoginResponse).token;
    if (token) {
      localStorage.setItem(TOKEN_KEY, token);
    }
    const user = this.toUser(res);
    localStorage.setItem(USER_KEY, JSON.stringify(user));
    this.userSubject.next(user);
    if (user.companies?.length) {
      this.companyService.setCompanies(user.companies, user.companyId ?? undefined);
    }
  }

  private persistSession(res: LoginResponse): void {
    localStorage.setItem(TOKEN_KEY, res.token);
    const user = this.toUser(res);
    localStorage.setItem(USER_KEY, JSON.stringify(user));
    this.userSubject.next(user);
    if (res.companies?.length) {
      this.companyService.setCompanies(res.companies, res.companyId ?? undefined);
    } else if (res.companyId) {
      this.companyService.setActiveCompanyId(res.companyId);
    }
  }

  private toUser(res: LoginResponse | AuthUser): AuthUser {
    return {
      id: String(res.id),
      firstName: res.firstName,
      lastName: res.lastName,
      username: res.username,
      role: res.role,
      isAdmin: !!res.isAdmin,
      permissions: res.permissions ?? [],
      companyId: res.companyId,
      customerId: (res as LoginResponse).customerId ?? (res as AuthUser).customerId ?? null,
      companyName: res.companyName,
      companies: res.companies
    };
  }

  private readStoredUser(): AuthUser | null {
    const raw = localStorage.getItem(USER_KEY);
    if (!raw) return null;
    try {
      return JSON.parse(raw) as AuthUser;
    } catch {
      return null;
    }
  }
}
