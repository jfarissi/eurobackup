import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface PlateCompatibleProduct {
  id: number;
  erpProductId: string;
  name?: string;
  reference?: string;
  brand?: string;
  priceHT?: number;
  stockQuantity?: number;
  imageUrl?: string;
  categoryName?: string;
}

export interface PlateScanResult {
  plateNumber: string;
  country?: string;
  vin?: string;
  make?: string;
  model?: string;
  year?: number;
  engineCode?: string;
  fuelType?: string;
  powerHP?: number;
  isDemoData: boolean;
  message?: string;
  compatibleProducts: PlateCompatibleProduct[];
}

export interface PlateHistoryItem {
  id: string;
  plateNumber: string;
  country?: string;
  vin?: string;
  make?: string;
  model?: string;
  year?: number;
  productsFound: number;
  searchedAt: string;
}

@Injectable({ providedIn: 'root' })
export class PlateScanService {
  private readonly apiUrl = '/api/autoparts/plate';

  constructor(private http: HttpClient) {}

  scanPlate(imageFile: File): Observable<PlateScanResult> {
    const formData = new FormData();
    formData.append('image', imageFile);
    return this.http.post<PlateScanResult>(`${this.apiUrl}/scan`, formData);
  }

  searchByPlate(plateNumber: string, country?: string): Observable<PlateScanResult> {
    let params = new HttpParams().set('plate', plateNumber);
    if (country) params = params.set('country', country);
    return this.http.get<PlateScanResult>(`${this.apiUrl}/search`, { params });
  }

  searchByVin(vin: string): Observable<PlateScanResult> {
    return this.http.get<PlateScanResult>(`${this.apiUrl}/vin/${encodeURIComponent(vin)}`);
  }

  getHistory(limit = 20): Observable<PlateHistoryItem[]> {
    const params = new HttpParams().set('limit', String(limit));
    return this.http.get<PlateHistoryItem[]>(`${this.apiUrl}/history`, { params });
  }
}
