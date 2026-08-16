import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { PermissionService } from '../services/permission.service';
import { PermissionCode } from '../constants/permissions';
import { AuthService } from '../services/auth.service';

export function permissionGuard(...required: PermissionCode[]): CanActivateFn {
  return (_route, state) => {
    const auth = inject(AuthService);
    const permissions = inject(PermissionService);
    const router = inject(Router);

    if (!auth.isLoggedIn) {
      return router.createUrlTree(['/login']);
    }

    const currentPath = state.url.split('?')[0];
    const allowed = required.length === 0 || permissions.hasAny(...required);

    if (allowed) return true;

    const fallback = permissions.getDefaultHomeUrl(currentPath);
    if (fallback === currentPath) {
      return router.createUrlTree(['/access-denied']);
    }
    return router.createUrlTree([fallback]);
  };
}

/** Empêche un compte Garage d'ouvrir les écrans staff (dashboard / assistant). */
export function denyGaragePortalGuard(): CanActivateFn {
  return () => {
    const permissions = inject(PermissionService);
    const router = inject(Router);
    if (permissions.isGaragePortalUser()) {
      return router.createUrlTree(['/garage']);
    }
    return true;
  };
}
