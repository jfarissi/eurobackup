import { Injectable } from '@angular/core';
import { AuthService } from './auth.service';
import { PermissionCode, RoutePermissions } from '../constants/permissions';

/** Ordre de priorité pour la page d'accueil après login. */
const HOME_CANDIDATES = [
  '/dashboard',
  '/sales',
  '/purchases',
  '/upload',
  '/stock',
  '/erp-products',
  '/cash',
  '/accounting',
  '/recherche',
  '/compare',
  '/erp-changes',
  '/numbering',
  '/admin',
] as const;

@Injectable({ providedIn: 'root' })
export class PermissionService {
  constructor(private auth: AuthService) {}

  has(permission: PermissionCode | string): boolean {
    const user = this.auth.currentUser;
    if (!user) return false;
    if (user.isAdmin || user.role?.toLowerCase() === 'admin') return true;
    if (!permission) return false;
    const needle = String(permission).toLowerCase();
    return (user.permissions ?? []).some(p => String(p).toLowerCase() === needle);
  }

  hasAny(...permissions: (PermissionCode | string)[]): boolean {
    if (permissions.length === 0) return true;
    return permissions.some(p => this.has(p));
  }

  hasAll(...permissions: (PermissionCode | string)[]): boolean {
    return permissions.every(p => this.has(p));
  }

  canAccessRoute(path: string): boolean {
    const required = RoutePermissions[path];
    if (!required?.length) return true;
    return this.hasAny(...required);
  }

  /** Première route accessible, sinon /access-denied. */
  getDefaultHomeUrl(excludePath?: string): string {
    for (const path of HOME_CANDIDATES) {
      if (excludePath && path === excludePath) continue;
      if (this.canAccessRoute(path)) return path;
    }
    return '/access-denied';
  }
}
