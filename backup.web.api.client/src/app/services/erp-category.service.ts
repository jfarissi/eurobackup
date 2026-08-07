import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { ErpCategory } from '../models/erp-product';

export interface ErpCategoryForm {
  id?: number;
  level: string;
  parentId?: number | null;
  erpExternalId?: string;
  nameNl?: string;
  nameFr?: string;
  nameEn?: string;
  slugNl?: string;
  slugFr?: string;
  slugEn?: string;
  sortOrder?: number;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class ErpCategoryService {
  private baseUrl = `${environment.apiBaseUrl}/erp-categories`;

  constructor(private http: HttpClient) {}

  list(query: { level?: string; parentId?: number; activeOnly?: boolean } = {}): Observable<ErpCategory[]> {
    let params = new HttpParams();
    if (query.level) params = params.set('level', query.level);
    if (query.parentId != null) params = params.set('parentId', String(query.parentId));
    if (query.activeOnly === true) params = params.set('activeOnly', 'true');
    return this.http.get<ErpCategory[]>(this.baseUrl, { params });
  }

  getById(id: number): Observable<ErpCategory> {
    return this.http.get<ErpCategory>(`${this.baseUrl}/${id}`);
  }

  create(body: ErpCategoryForm): Observable<ErpCategory> {
    return this.http.post<ErpCategory>(this.baseUrl, body);
  }

  update(id: number, body: ErpCategoryForm): Observable<ErpCategory> {
    return this.http.put<ErpCategory>(`${this.baseUrl}/${id}`, body);
  }

  deactivate(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
