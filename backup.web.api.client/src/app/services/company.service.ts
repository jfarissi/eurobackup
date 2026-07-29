import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { Company } from '../models/company';

const COMPANY_KEY = 'backup_active_company_id';

@Injectable({ providedIn: 'root' })
export class CompanyService {
  private readonly companiesSubject = new BehaviorSubject<Company[]>([]);
  private readonly activeCompanyIdSubject = new BehaviorSubject<string | null>(this.readStoredCompanyId());

  readonly companies$ = this.companiesSubject.asObservable();
  readonly activeCompanyId$ = this.activeCompanyIdSubject.asObservable();

  constructor(private http: HttpClient) {}

  get activeCompanyId(): string | null {
    return this.activeCompanyIdSubject.value;
  }

  get companies(): Company[] {
    return this.companiesSubject.value;
  }

  loadAvailable(): Observable<Company[]> {
    return this.http.get<Company[]>('/api/companies/available').pipe(
      tap(companies => {
        this.companiesSubject.next(companies);
        if (!this.activeCompanyId && companies.length > 0) {
          this.setActiveCompanyId(companies[0].id);
        }
      })
    );
  }

  setCompanies(companies: Company[], activeId?: string | null): void {
    this.companiesSubject.next(companies);
    if (activeId) {
      this.setActiveCompanyId(activeId);
    } else if (!this.activeCompanyId && companies.length > 0) {
      this.setActiveCompanyId(companies[0].id);
    }
  }

  setActiveCompanyId(companyId: string | null): void {
    this.activeCompanyIdSubject.next(companyId);
    if (companyId) {
      localStorage.setItem(COMPANY_KEY, companyId);
    } else {
      localStorage.removeItem(COMPANY_KEY);
    }
  }

  activeCompanyName(): string {
    const id = this.activeCompanyId;
    return this.companies.find(c => c.id === id)?.name ?? '';
  }

  private readStoredCompanyId(): string | null {
    return localStorage.getItem(COMPANY_KEY);
  }
}
