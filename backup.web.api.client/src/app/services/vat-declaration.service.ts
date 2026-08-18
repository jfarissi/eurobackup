import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { VatDeclaration } from '../models/accounting';

/** Accès API déclaration TVA mensuelle (api/vat-declarations). */
@Injectable({
  providedIn: 'root'
})
export class VatDeclarationApiService {
  constructor(private http: HttpClient) {}

  get(year: number, month: number): Observable<VatDeclaration> {
    const params = new HttpParams().set('year', year).set('month', month);
    return this.http.get<VatDeclaration>('/api/vat-declarations', { params });
  }

  declare(year: number, month: number): Observable<VatDeclaration> {
    return this.http.post<VatDeclaration>('/api/vat-declarations/declare', { year, month });
  }

  undeclare(year: number, month: number): Observable<{ undeclared: boolean }> {
    const params = new HttpParams().set('year', year).set('month', month);
    return this.http.delete<{ undeclared: boolean }>('/api/vat-declarations', { params });
  }

  downloadEdi(year: number, month: number) {
    const params = new HttpParams().set('year', year).set('month', month);
    return this.http.get('/api/vat-declarations/edi', { params, responseType: 'blob', observe: 'response' as const });
  }
}
