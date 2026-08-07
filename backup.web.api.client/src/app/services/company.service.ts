import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap, catchError, of } from 'rxjs';
import { Company } from '../models/company';

export interface CompanyModule {
  id: string;
  companyId: string;
  moduleCode: string;
  moduleName: string;
  isActive: boolean;
  configJson?: string | null;
  activatedAt?: string;
  expiresAt?: string | null;
}

export const ModuleCodes = {
  Core: 'core',
  ErpCatalogSync: 'erp_catalog_sync',
  AutoParts: 'auto_parts',
  Hardware: 'hardware',
  Appliances: 'appliances'
} as const;

const COMPANY_KEY = 'backup_active_company_id';

@Injectable({ providedIn: 'root' })
export class CompanyService {
  private readonly companiesSubject = new BehaviorSubject<Company[]>([]);
  private readonly activeCompanyIdSubject = new BehaviorSubject<string | null>(this.readStoredCompanyId());
  private readonly modulesSubject = new BehaviorSubject<CompanyModule[]>([]);

  readonly companies$ = this.companiesSubject.asObservable();
  readonly activeCompanyId$ = this.activeCompanyIdSubject.asObservable();
  readonly modules$ = this.modulesSubject.asObservable();

  constructor(private http: HttpClient) {}

  get activeCompanyId(): string | null {
    return this.activeCompanyIdSubject.value;
  }

  get companies(): Company[] {
    return this.companiesSubject.value;
  }

  get modules(): CompanyModule[] {
    return this.modulesSubject.value;
  }

  loadAvailable(): Observable<Company[]> {
    return this.http.get<Company[]>('/api/companies/available').pipe(
      tap(companies => {
        this.companiesSubject.next(companies);
        if (!this.activeCompanyId && companies.length > 0) {
          this.setActiveCompanyId(companies[0].id);
        }
        this.loadModules().subscribe();
      })
    );
  }

  setCompanies(companies: Company[], activeId?: string | null): void {
    this.companiesSubject.next(
      (companies ?? []).map(c => ({
        ...c,
        enableErpCatalogSync: !!(c as Company).enableErpCatalogSync
      }))
    );
    if (activeId) {
      this.setActiveCompanyId(activeId);
    } else if (!this.activeCompanyId && companies.length > 0) {
      this.setActiveCompanyId(companies[0].id);
    } else {
      this.loadModules().subscribe();
    }
  }

  setActiveCompanyId(companyId: string | null): void {
    this.activeCompanyIdSubject.next(companyId);
    if (companyId) {
      localStorage.setItem(COMPANY_KEY, companyId);
    } else {
      localStorage.removeItem(COMPANY_KEY);
      this.modulesSubject.next([]);
    }
    this.loadModules().subscribe();
  }

  activeCompanyName(): string {
    const id = this.activeCompanyId;
    return this.companies.find(c => c.id === id)?.name ?? '';
  }

  /** Sync catalogue ERP (Euro Brico) : flag société OU module erp_catalog_sync. */
  get hasErpCatalogSync(): boolean {
    const id = this.activeCompanyId;
    const fromCompany = !!this.companies.find(c => c.id === id)?.enableErpCatalogSync;
    return fromCompany || this.hasModule(ModuleCodes.ErpCatalogSync);
  }

  get hasAutoParts(): boolean {
    return this.hasModule(ModuleCodes.AutoParts);
  }

  hasModule(code: string): boolean {
    return this.modules.some(m => m.moduleCode === code && m.isActive);
  }

  loadModules(): Observable<CompanyModule[]> {
    if (!this.activeCompanyId) {
      this.modulesSubject.next([]);
      return of([]);
    }
    return this.http.get<CompanyModule[]>('/api/company-modules').pipe(
      tap(mods => this.modulesSubject.next(mods ?? [])),
      catchError(() => {
        this.modulesSubject.next([]);
        return of([]);
      })
    );
  }

  private readStoredCompanyId(): string | null {
    return localStorage.getItem(COMPANY_KEY);
  }
}
