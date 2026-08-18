import { Injectable } from '@angular/core';
import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';

/** Miroir de AccountingExportsService.ExportPreviewDto. */
export interface AccountingExportPreview {
  fiscalYearId: number;
  yearName: string;
  from: string;
  to: string;
  entryCount: number;
  lineCount: number;
}

/** Accès API exports FEC / CSV (api/accounting-exports). */
@Injectable({
  providedIn: 'root'
})
export class AccountingExportsApiService {
  constructor(private http: HttpClient) {}

  preview(yearId: number): Observable<AccountingExportPreview> {
    const params = new HttpParams().set('yearId', yearId);
    return this.http.get<AccountingExportPreview>('/api/accounting-exports/preview', { params });
  }

  downloadFec(yearId: number): Observable<HttpResponse<Blob>> {
    const params = new HttpParams().set('yearId', yearId);
    return this.http.get('/api/accounting-exports/fec', { params, responseType: 'blob', observe: 'response' });
  }

  downloadCsv(yearId: number): Observable<HttpResponse<Blob>> {
    const params = new HttpParams().set('yearId', yearId);
    return this.http.get('/api/accounting-exports/csv', { params, responseType: 'blob', observe: 'response' });
  }
}
