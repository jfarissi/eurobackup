import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { FiscalPeriod, FiscalYear } from '../models/accounting';

export interface ClosingCheck {
  code: string;
  severity: 'Blocking' | 'Warning';
  message: string;
}

export interface ClosingPreview {
  fiscalYearId: number;
  yearName: string;
  status: string;
  startDate: string;
  endDate: string;
  canClose: boolean;
  profit: number;
  resultAccountCode: string;
  resultAccountsToClose: number;
  bilanAccountsToCarry: number;
  nextYearId?: number | null;
  nextYearName?: string | null;
  checks: ClosingCheck[];
}

export interface CloseYearResult {
  success: boolean;
  error?: string | null;
  closeEntryNumber?: string | null;
  carryForwardEntryNumber?: string | null;
  nextYearId?: number | null;
  preview?: ClosingPreview | null;
}

@Injectable({
  providedIn: 'root'
})
export class FiscalClosingService {
  constructor(private http: HttpClient) {}

  preview(yearId: number): Observable<ClosingPreview> {
    return this.http.get<ClosingPreview>(`/api/fiscal-closing/years/${yearId}/preview`);
  }

  closePeriod(periodId: number): Observable<FiscalPeriod> {
    return this.http.post<FiscalPeriod>(`/api/fiscal-closing/periods/${periodId}/close`, {});
  }

  closeYear(yearId: number): Observable<CloseYearResult> {
    return this.http.post<CloseYearResult>(`/api/fiscal-closing/years/${yearId}/close`, {});
  }

  openNext(yearId: number): Observable<FiscalYear> {
    return this.http.post<FiscalYear>(`/api/fiscal-closing/years/${yearId}/open-next`, {});
  }
}
