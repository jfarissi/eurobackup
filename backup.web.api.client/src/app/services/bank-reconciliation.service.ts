import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { BankMatchResult, BankReconciliation } from '../models/accounting';

/** Accès API rapprochement bancaire (api/bank-reconciliations). */
@Injectable({
  providedIn: 'root'
})
export class BankReconciliationApiService {
  constructor(private http: HttpClient) {}

  list(): Observable<BankReconciliation[]> {
    return this.http.get<BankReconciliation[]>('/api/bank-reconciliations');
  }

  get(id: number): Observable<BankReconciliation> {
    return this.http.get<BankReconciliation>(`/api/bank-reconciliations/${id}`);
  }

  import(file: File, accountCode?: string): Observable<BankReconciliation> {
    const form = new FormData();
    form.append('file', file);
    if (accountCode?.trim()) form.append('accountCode', accountCode.trim());
    return this.http.post<BankReconciliation>('/api/bank-reconciliations/import', form);
  }

  autoMatch(id: number): Observable<BankMatchResult> {
    return this.http.post<BankMatchResult>(`/api/bank-reconciliations/${id}/match`, {});
  }

  manualMatch(id: number, lineId: number, accountingEntryLineId: number): Observable<BankReconciliation> {
    return this.http.post<BankReconciliation>(
      `/api/bank-reconciliations/${id}/lines/${lineId}/match`,
      { accountingEntryLineId }
    );
  }

  unmatch(id: number, lineId: number): Observable<BankReconciliation> {
    return this.http.delete<BankReconciliation>(`/api/bank-reconciliations/${id}/lines/${lineId}/match`);
  }

  complete(id: number): Observable<BankReconciliation> {
    return this.http.post<BankReconciliation>(`/api/bank-reconciliations/${id}/complete`, {});
  }
}
