import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { ProductDiagram } from '../models/product-diagram';

@Injectable({ providedIn: 'root' })
export class ProductDiagramService {
  private readonly baseUrl = `${environment.apiBaseUrl}/product-diagrams`;

  constructor(private http: HttpClient) {}

  getByProduct(productId: number): Observable<ProductDiagram[]> {
    return this.http.get<ProductDiagram[]>(`${this.baseUrl}/${productId}`);
  }
}
