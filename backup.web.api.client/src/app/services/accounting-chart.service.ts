import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  ChartOfAccount,
  ChartOfAccountClassGroup,
  ChartOfAccountForm,
  FiscalPeriod,
  FiscalYear,
  Journal,
  JournalForm,
  OpenFiscalYearRequest
} from '../models/accounting';

/** Accès API Phase 1 comptabilité : plan comptable, journaux, exercices. */
@Injectable({
  providedIn: 'root'
})
export class AccountingChartService {
  constructor(private http: HttpClient) {}

  // Plan comptable
  getAccounts(accountClass?: number | null, search?: string): Observable<ChartOfAccount[]> {
    let params = new HttpParams();
    if (accountClass) params = params.set('accountClass', accountClass);
    if (search) params = params.set('search', search);
    return this.http.get<ChartOfAccount[]>('/api/chart-of-accounts', { params });
  }

  getAccountTree(): Observable<ChartOfAccountClassGroup[]> {
    return this.http.get<ChartOfAccountClassGroup[]>('/api/chart-of-accounts/tree');
  }

  createAccount(account: ChartOfAccountForm): Observable<ChartOfAccount> {
    return this.http.post<ChartOfAccount>('/api/chart-of-accounts', account);
  }

  updateAccount(id: number, account: ChartOfAccountForm): Observable<ChartOfAccount> {
    return this.http.put<ChartOfAccount>(`/api/chart-of-accounts/${id}`, account);
  }

  deleteAccount(id: number): Observable<void> {
    return this.http.delete<void>(`/api/chart-of-accounts/${id}`);
  }

  // Journaux
  getJournals(): Observable<Journal[]> {
    return this.http.get<Journal[]>('/api/journals');
  }

  createJournal(journal: JournalForm): Observable<Journal> {
    return this.http.post<Journal>('/api/journals', journal);
  }

  // Exercices
  getFiscalYears(): Observable<FiscalYear[]> {
    return this.http.get<FiscalYear[]>('/api/fiscal-years');
  }

  openFiscalYear(request: OpenFiscalYearRequest): Observable<FiscalYear> {
    return this.http.post<FiscalYear>('/api/fiscal-years/open', request);
  }

  lockPeriod(id: number): Observable<FiscalPeriod> {
    return this.http.post<FiscalPeriod>(`/api/fiscal-years/periods/${id}/lock`, {});
  }

  unlockPeriod(id: number): Observable<FiscalPeriod> {
    return this.http.post<FiscalPeriod>(`/api/fiscal-years/periods/${id}/unlock`, {});
  }
}
