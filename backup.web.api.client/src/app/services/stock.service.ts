import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { StockItem } from '../models/stock-item';

@Injectable({ providedIn: 'root' })
export class StockService {
  private baseUrl = `${environment.apiBaseUrl}/stock`;

  constructor(private http: HttpClient) {}

  getAll(search?: string): Observable<StockItem[]> {
    let params = new HttpParams();
    if (search) {
      params = params.set('search', search);
    }
    return this.http.get<StockItem[]>(this.baseUrl, { params });
  }

  getById(id: number): Observable<StockItem> {
    return this.http.get<StockItem>(`${this.baseUrl}/${id}`);
  }
}

