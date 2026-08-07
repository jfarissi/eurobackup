import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { ErpBrand } from '../models/erp-product';

export interface ErpBrandForm {
  id?: number;
  name: string;
  slug?: string;
  logoUrl?: string | null;
  websiteUrl?: string | null;
  description?: string | null;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class ErpBrandService {
  private baseUrl = `${environment.apiBaseUrl}/erp-brands`;

  constructor(private http: HttpClient) {}

  list(activeOnly?: boolean): Observable<ErpBrand[]> {
    let params = new HttpParams();
    if (activeOnly === true) params = params.set('activeOnly', 'true');
    return this.http.get<ErpBrand[]>(this.baseUrl, { params });
  }

  getById(id: number): Observable<ErpBrand> {
    return this.http.get<ErpBrand>(`${this.baseUrl}/${id}`);
  }

  create(body: ErpBrandForm): Observable<ErpBrand> {
    return this.http.post<ErpBrand>(this.baseUrl, body);
  }

  update(id: number, body: ErpBrandForm): Observable<ErpBrand> {
    return this.http.put<ErpBrand>(`${this.baseUrl}/${id}`, body);
  }

  deactivate(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
