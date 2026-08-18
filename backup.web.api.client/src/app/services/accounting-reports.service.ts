import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { BalanceReport, LedgerReport } from '../models/accounting';

/** Accès API rapports comptables : balance des comptes et grand livre. */
@Injectable({
  providedIn: 'root'
})
export class AccountingReportsService {
  constructor(private http: HttpClient) {}

  getBalance(from?: string, to?: string): Observable<BalanceReport> {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return this.http.get<BalanceReport>('/api/accounting-reports/balance', { params });
  }

  getGeneralLedger(accountCode: string, from?: string, to?: string): Observable<LedgerReport> {
    let params = new HttpParams().set('accountCode', accountCode);
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return this.http.get<LedgerReport>('/api/accounting-reports/general-ledger', { params });
  }
}
