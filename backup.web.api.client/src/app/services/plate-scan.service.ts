import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { map, Observable } from 'rxjs';

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
  vehicleMake?: string;
  vehicleModel?: string;
  vehicleTypeName?: string;
  yearFrom?: number;
  yearTo?: number;
  engineCode?: string;
  kType?: string;
  fuelType?: string;
  oemCount?: number;
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
  kType?: string;
  isDemoData: boolean;
  fromRegistry: boolean;
  needsVehicleLink: boolean;
  ocrProvider?: string;
  ocrScore?: number;
  message?: string;
  /** KType | MakeModel | None */
  productMatchMode?: string;
  kTypeInCatalog: boolean;
  kTypeEnrichmentQueued: boolean;
  kTypeSyncInProgress?: boolean;
  needsCategorySelection?: boolean;
  compatibleProducts: PlateCompatibleProduct[];
}

export interface RapidApiCategory {
  id: number;
  name: string;
  family: string;
  familyLabel: string;
  parentName?: string;
}

export interface RapidApiCategoryList {
  kType: string;
  categories: RapidApiCategory[];
}

export interface KTypeCategoryImportRequest {
  kType: string;
  make?: string;
  model?: string;
  year?: number;
  vin?: string;
  fuelType?: string;
  categoryIds: number[];
}

export interface KTypeSyncProgress {
  kType: string;
  status: 'Idle' | 'Running' | 'Done' | 'Failed';
  phase?: string;
  current: number;
  total: number;
  percent: number;
  message?: string;
  productsImported?: number;
  updatedAt?: string;
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

export interface LinkPlateVinRequest {
  plate: string;
  country?: string;
  vin: string;
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

  linkPlateToVin(body: LinkPlateVinRequest): Observable<PlateScanResult> {
    return this.http.post<PlateScanResult>(`${this.apiUrl}/link`, body);
  }

  getHistory(limit = 20): Observable<PlateHistoryItem[]> {
    const params = new HttpParams().set('limit', String(limit));
    return this.http.get<PlateHistoryItem[]>(`${this.apiUrl}/history`, { params });
  }

  getKTypeSyncProgress(kType: string): Observable<KTypeSyncProgress> {
    return this.http.get<KTypeSyncProgress>(
      `${this.apiUrl}/ktype-sync/progress/${encodeURIComponent(kType)}`
    ).pipe(
      map((p) => ({
        ...p,
        status: normalizeKTypeSyncStatus((p as { status?: unknown })?.status)
      }))
    );
  }

  listKTypeCategories(kType: string): Observable<RapidApiCategoryList> {
    return this.http.get<RapidApiCategoryList>(
      `${this.apiUrl}/ktype-sync/categories/${encodeURIComponent(kType)}`
    );
  }

  importKTypeCategories(body: KTypeCategoryImportRequest): Observable<{ syncInProgress?: boolean; message?: string }> {
    return this.http.post<{ syncInProgress?: boolean; message?: string }>(
      `${this.apiUrl}/ktype-sync/import`,
      body
    );
  }
}

/** L'API peut renvoyer l'enum en string ou en int (0–3) selon la config JSON. */
function normalizeKTypeSyncStatus(status: unknown): KTypeSyncProgress['status'] {
  if (status === 1 || status === 'Running' || status === 'running') return 'Running';
  if (status === 2 || status === 'Done' || status === 'done') return 'Done';
  if (status === 3 || status === 'Failed' || status === 'failed') return 'Failed';
  return 'Idle';
}
