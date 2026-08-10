// ============================================================
// src/app/features/auto-parts/services/plate-scan.service.ts
// Service de lecture de plaque d'immatriculation + décodage VIN
// ============================================================

import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

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
  compatibleProducts: PlateCompatibleProduct[];
}

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

export interface PlateHistoryItem {
  id: string;
  plateNumber: string;
  vin?: string;
  make?: string;
  model?: string;
  year?: number;
  searchedAt: string;
  productsFound: number;
}

@Injectable({ providedIn: 'root' })
export class PlateScanService {
  private readonly apiUrl = '/api/autoparts/plate';

  constructor(private http: HttpClient) {}

  /** Analyse une image de plaque et retourne le véhicule + pièces compatibles */
  scanPlate(imageFile: File): Observable<PlateScanResult> {
    const formData = new FormData();
    formData.append('image', imageFile);
    return this.http.post<PlateScanResult>(`${this.apiUrl}/scan`, formData);
  }

  /** Recherche par numéro de plaque (texte) */
  searchByPlate(plateNumber: string, country?: string): Observable<PlateScanResult> {
    return this.http.get<PlateScanResult>(`${this.apiUrl}/search`, {
      params: { plate: plateNumber, ...(country ? { country } : {}) }
    });
  }

  /** Historique des recherches par plaque pour la société */
  getHistory(): Observable<PlateHistoryItem[]> {
    return this.http.get<PlateHistoryItem[]>(`${this.apiUrl}/history`);
  }

  /** Recherche par VIN directement */
  searchByVin(vin: string): Observable<PlateScanResult> {
    return this.http.get<PlateScanResult>(`${this.apiUrl}/vin/${encodeURIComponent(vin)}`);
  }
}
