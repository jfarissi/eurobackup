import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { ErpProductAttributeDefinition } from './erp-catalog-extras.service';

export interface CarApiVehicleBrand {
  brand: string;
  modelCount: number;
}

export interface CarApiVehicleModel {
  name: string;
  generationCount: number;
}

export interface CarApiVehicleGeneration {
  name: string;
  yearFrom?: number | null;
  yearTo?: number | null;
}

export interface VehicleCompatibilityEntry {
  brand: string;
  model: string;
  generation: string;
  yearFrom?: number | null;
  yearTo?: number | null;
}

export interface CarApiImportResult {
  partsTotal: number;
  partsCreated: number;
  partsUpdated: number;
  partsSkipped: number;
  variantsCreated: number;
  vehicleBrandsTotal: number;
  vehicleBrandsCreated: number;
  vehicleBrandsSkipped: number;
  categoriesCreated: number;
  frenchNamesUpdated: number;
  vehicleAttributeEnsured: boolean;
  errors: string[];
}

@Injectable({ providedIn: 'root' })
export class CarApiService {
  private baseUrl = `${environment.apiBaseUrl}/car-api`;

  constructor(private http: HttpClient) {}

  getBrands(): Observable<CarApiVehicleBrand[]> {
    return this.http.get<CarApiVehicleBrand[]>(`${this.baseUrl}/brands`);
  }

  getModels(brand: string): Observable<CarApiVehicleModel[]> {
    const params = new HttpParams().set('brand', brand);
    return this.http.get<CarApiVehicleModel[]>(`${this.baseUrl}/models`, { params });
  }

  getGenerations(brand: string, model: string): Observable<CarApiVehicleGeneration[]> {
    const params = new HttpParams().set('brand', brand).set('model', model);
    return this.http.get<CarApiVehicleGeneration[]>(`${this.baseUrl}/generations`, { params });
  }

  ensureVehicleAttribute(): Observable<ErpProductAttributeDefinition> {
    return this.http.post<ErpProductAttributeDefinition>(`${this.baseUrl}/ensure-vehicle-attribute`, {});
  }
}
