// ============================================================
// src/app/core/services/module.service.ts
// Gère les modules actifs de la société connectée
// ============================================================

import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, of } from 'rxjs';
import { map, shareReplay, tap, catchError } from 'rxjs/operators';
import { ErpCompanyModule, AutoPartsModuleConfig } from '../models';

@Injectable({ providedIn: 'root' })
export class ModuleService {
  private readonly apiUrl = '/api/admin/modules';
  private modules$ = new BehaviorSubject<ErpCompanyModule[]>([]);
  private loaded = false;

  constructor(private http: HttpClient) {}

  /** Charge les modules pour la société courante (appelé au login) */
  loadModules(companyId: string): Observable<ErpCompanyModule[]> {
    return this.http.get<ErpCompanyModule[]>(`${this.apiUrl}/${companyId}`).pipe(
      tap(modules => {
        this.modules$.next(modules);
        this.loaded = true;
      }),
      shareReplay(1),
      catchError(err => {
        console.error('Erreur chargement modules:', err);
        return of([]);
      })
    );
  }

  /** Retourne tous les modules actifs */
  getActiveModules(): Observable<ErpCompanyModule[]> {
    return this.modules$.asObservable().pipe(
      map(modules => modules.filter(m => m.isActive))
    );
  }

  /** Vérifie si un module est actif */
  hasModule(moduleCode: string): Observable<boolean> {
    return this.modules$.pipe(
      map(modules => modules.some(m => m.moduleCode === moduleCode && m.isActive))
    );
  }

  /** Vérifie si au moins un des modules est actif */
  hasAnyModule(...moduleCodes: string[]): Observable<boolean> {
    return this.modules$.pipe(
      map(modules => modules.some(m => moduleCodes.includes(m.moduleCode) && m.isActive))
    );
  }

  /** Récupère la config JSON typée d'un module */
  getModuleConfig<T>(moduleCode: string): Observable<T | null> {
    return this.modules$.pipe(
      map(modules => {
        const mod = modules.find(m => m.moduleCode === moduleCode && m.isActive);
        if (!mod?.configJson) return null;
        try {
          return JSON.parse(mod.configJson) as T;
        } catch {
          return null;
        }
      })
    );
  }

  getAutoPartsConfig(): Observable<AutoPartsModuleConfig | null> {
    return this.getModuleConfig<AutoPartsModuleConfig>('auto_parts');
  }
}
