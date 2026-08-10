// ============================================================
// src/app/core/guards/module.guard.ts
// Empêche l'accès aux routes si le module n'est pas actif
// ============================================================

import { Injectable } from '@angular/core';
import { CanActivate, Router, ActivatedRouteSnapshot } from '@angular/router';
import { Observable, of } from 'rxjs';
import { map, take, catchError } from 'rxjs/operators';
import { ModuleService } from '../services/module.service';

@Injectable({ providedIn: 'root' })
export class ModuleGuard implements CanActivate {
  constructor(private moduleService: ModuleService, private router: Router) {}

  canActivate(route: ActivatedRouteSnapshot): Observable<boolean> {
    const requiredModule = route.data['requiredModule'] as string;
    if (!requiredModule) return of(true);

    return this.moduleService.hasModule(requiredModule).pipe(
      take(1),
      map(hasIt => {
        if (!hasIt) {
          this.router.navigate(['/unauthorized']);
          return false;
        }
        return true;
      }),
      catchError(() => {
        this.router.navigate(['/unauthorized']);
        return of(false);
      })
    );
  }
}
