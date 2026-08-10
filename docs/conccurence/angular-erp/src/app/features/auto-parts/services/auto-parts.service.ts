// ============================================================
// src/app/features/auto-parts/services/auto-parts.service.ts
// Service spécifique pièces auto (nécessite le module)
// ============================================================

import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Product, SyncRequest, SyncResult } from '../../../core/models';

@Injectable({ providedIn: 'root' })
export class AutoPartsService {
  private readonly apiUrl = '/api/autoparts';

  constructor(private http: HttpClient) {}

  searchByOem(oemNumber: string): Observable<Product> {
    return this.http.get<Product>(`${this.apiUrl}/search/oem/${encodeURIComponent(oemNumber)}`);
  }

  searchByReference(reference: string): Observable<Product> {
    return this.http.get<Product>(`${this.apiUrl}/search/reference/${encodeURIComponent(reference)}`);
  }

  searchByVehicle(make: string, model: string, year?: number): Observable<Product[]> {
    const params: any = { make, model };
    if (year) params.year = year;
    return this.http.get<Product[]>(`${this.apiUrl}/search/vehicle`, { params });
  }

  syncCatalog(request: SyncRequest): Observable<SyncResult> {
    return this.http.post<SyncResult>(`${this.apiUrl}/sync`, request);
  }
}
