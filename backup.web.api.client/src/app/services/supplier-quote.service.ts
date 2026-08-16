import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { SupplierQuotesResult } from '../models/supplier-quote';

@Injectable({ providedIn: 'root' })
export class SupplierQuoteService {
  private readonly baseUrl = `${environment.apiBaseUrl}/supplier-quotes`;

  constructor(private http: HttpClient) {}

  get(productId: number): Observable<SupplierQuotesResult> {
    return this.http.get<SupplierQuotesResult>(`${this.baseUrl}/${productId}`);
  }

  refresh(productId: number): Observable<SupplierQuotesResult> {
    return this.http.post<SupplierQuotesResult>(`${this.baseUrl}/${productId}/refresh`, {});
  }
}
