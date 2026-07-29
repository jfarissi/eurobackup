import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { PermissionService } from '../services/permission.service';

/** Redirige '' vers la première page autorisée pour l'utilisateur. */
export const homeRedirectGuard: CanActivateFn = () => {
  const perm = inject(PermissionService);
  const router = inject(Router);
  return router.createUrlTree([perm.getDefaultHomeUrl()]);
};
