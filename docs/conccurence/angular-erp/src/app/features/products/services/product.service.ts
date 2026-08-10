// ============================================================
// src/app/features/products/services/product.service.ts
// Service générique produits (disponible pour TOUTES les sociétés)
// ============================================================

import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Product } from '../../../core/models';

@Injectable({ providedIn: 'root' })
export class ProductService {
  private readonly apiUrl = '/api/products';

  constructor(private http: HttpClient) {}

  getAll(): Observable<Product[]> {
    return this.http.get<Product[]>(this.apiUrl);
  }

  getById(id: number): Observable<Product> {
    return this.http.get<Product>(`${this.apiUrl}/${id}`);
  }

  search(query: string): Observable<Product[]> {
    return this.http.get<Product[]>(`${this.apiUrl}/search`, { params: { q: query } });
  }
}
