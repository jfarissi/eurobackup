import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface ErpProductVariant {
  id: string;
  productId: number;
  sku: string;
  barcode?: string | null;
  costPrice?: number | null;
  priceOverride?: number | null;
  stockQuantity: number;
  attributesJson: string;
  weight?: number | null;
  length?: number | null;
  width?: number | null;
  height?: number | null;
  isActive: boolean;
  createdAt?: string;
  updatedAt?: string | null;
}

export interface ErpProductImage {
  id: string;
  productId: number;
  url: string;
  altText: string;
  isMain: boolean;
  sortOrder: number;
  createdAt?: string;
  updatedAt?: string | null;
}

export interface ErpProductAttributeDefinition {
  id: string;
  companyId: string;
  code: string;
  name: string;
  isActive: boolean;
}

export interface ErpProductAttributeValue {
  id: string;
  productId: number;
  attributeId: string;
  value: string;
  attribute?: ErpProductAttributeDefinition | null;
}

@Injectable({ providedIn: 'root' })
export class ErpCatalogExtrasService {
  private variantsUrl = `${environment.apiBaseUrl}/erpproductvariants`;
  private imagesUrl = `${environment.apiBaseUrl}/erpproductimages`;
  private attrsUrl = `${environment.apiBaseUrl}/erpproduct-attributes`;

  constructor(private http: HttpClient) {}

  getVariants(productId: number): Observable<ErpProductVariant[]> {
    return this.http.get<ErpProductVariant[]>(`${this.variantsUrl}/product/${productId}`);
  }

  createVariant(body: Partial<ErpProductVariant>): Observable<ErpProductVariant> {
    return this.http.post<ErpProductVariant>(this.variantsUrl, body);
  }

  updateVariant(id: string, body: Partial<ErpProductVariant>): Observable<ErpProductVariant> {
    return this.http.put<ErpProductVariant>(`${this.variantsUrl}/${id}`, body);
  }

  deleteVariant(id: string): Observable<void> {
    return this.http.delete<void>(`${this.variantsUrl}/${id}`);
  }

  getImages(productId: number): Observable<ErpProductImage[]> {
    return this.http.get<ErpProductImage[]>(`${this.imagesUrl}/product/${productId}`);
  }

  createImage(body: Partial<ErpProductImage>): Observable<ErpProductImage> {
    return this.http.post<ErpProductImage>(this.imagesUrl, body);
  }

  updateImage(id: string, body: Partial<ErpProductImage>): Observable<ErpProductImage> {
    return this.http.put<ErpProductImage>(`${this.imagesUrl}/${id}`, body);
  }

  deleteImage(id: string): Observable<void> {
    return this.http.delete<void>(`${this.imagesUrl}/${id}`);
  }

  getAttributeDefinitions(): Observable<ErpProductAttributeDefinition[]> {
    return this.http.get<ErpProductAttributeDefinition[]>(`${this.attrsUrl}/definitions`);
  }

  createAttributeDefinition(body: Partial<ErpProductAttributeDefinition>): Observable<ErpProductAttributeDefinition> {
    return this.http.post<ErpProductAttributeDefinition>(`${this.attrsUrl}/definitions`, body);
  }

  updateAttributeDefinition(id: string, body: Partial<ErpProductAttributeDefinition>): Observable<ErpProductAttributeDefinition> {
    return this.http.put<ErpProductAttributeDefinition>(`${this.attrsUrl}/definitions/${id}`, body);
  }

  deleteAttributeDefinition(id: string): Observable<void> {
    return this.http.delete<void>(`${this.attrsUrl}/definitions/${id}`);
  }

  getAttributeValues(productId: number): Observable<ErpProductAttributeValue[]> {
    return this.http.get<ErpProductAttributeValue[]>(`${this.attrsUrl}/values/product/${productId}`);
  }

  upsertAttributeValue(body: { productId: number; attributeId: string; value: string }): Observable<ErpProductAttributeValue> {
    return this.http.post<ErpProductAttributeValue>(`${this.attrsUrl}/values`, body);
  }

  deleteAttributeValue(id: string): Observable<void> {
    return this.http.delete<void>(`${this.attrsUrl}/values/${id}`);
  }
}
