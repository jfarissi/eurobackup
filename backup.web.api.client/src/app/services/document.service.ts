import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Document } from '../models/document';
import { DocumentRelation } from '../models/relation';
import { ComparisonResult, ErpPriceDiffLine, InvoicePriceComparisonResult } from '../models/comparison';
import { BatchCompareAndStockResponse, CompareAndStockResponse } from '../models/stock';

@Injectable({ providedIn: 'root' })
export class DocumentService {
  private baseUrl = `${environment.apiBaseUrl}/documents`;

  constructor(private http: HttpClient) {}

  upload(file: File, typeDocument: string, numero?: string, client?: string, dateDocument?: string, supplier?: string): Observable<Document> {
    const form = new FormData();
    form.append('file', file);
    form.append('typeDocument', typeDocument);
    if (numero) form.append('numero', numero);
    if (client) form.append('client', client);
    if (supplier) form.append('supplier', supplier);
    if (dateDocument) form.append('dateDocument', dateDocument);
    return this.http.post<Document>(`${this.baseUrl}/upload`, form);
  }

  list(): Observable<Document[]> {
    return this.http.get<Document[]>(`${this.baseUrl}`);
  }

  search(q: string): Observable<Document[]> {
    const params = new HttpParams().set('q', q);
    return this.http.get<Document[]>(`${this.baseUrl}/search`, { params });
  }

  download(id: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/${id}/download`, { responseType: 'blob' });
  }

  link(invoiceId: number, deliveryId: number): Observable<DocumentRelation> {
    return this.http.post<DocumentRelation>(`${this.baseUrl}/link`, { invoiceId, deliveryId });
  }

  relations(): Observable<DocumentRelation[]> {
    return this.http.get<DocumentRelation[]>(`${this.baseUrl}/relations`);
  }

  unlink(relationId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/link/${relationId}`);
  }

  compare(invoiceId: number, deliveryId: number): Observable<ComparisonResult> {
    return this.http.post<ComparisonResult>(`${this.baseUrl}/compare?invoiceId=${invoiceId}&deliveryId=${deliveryId}`, {});
  }

  compareAllDeliveries(invoiceId: number): Observable<ComparisonResult> {
    return this.http.post<ComparisonResult>(`${this.baseUrl}/compare-all-deliveries?invoiceId=${invoiceId}`, {});
  }

  compareInvoices(invoice1Id: number, invoice2Id: number): Observable<InvoicePriceComparisonResult> {
    return this.http.post<InvoicePriceComparisonResult>(`${this.baseUrl}/compare-invoices?invoice1Id=${invoice1Id}&invoice2Id=${invoice2Id}`, {});
  }

  getErpPriceDiff(invoiceId: number): Observable<ErpPriceDiffLine[]> {
    return this.http.get<ErpPriceDiffLine[]>(`${this.baseUrl}/${invoiceId}/erp-price-diff`);
  }

  compareAndStock(invoiceId: number, deliveryId: number, forceUpdate: boolean = false): Observable<CompareAndStockResponse> {
    const params = new HttpParams()
      .set('invoiceId', invoiceId.toString())
      .set('deliveryId', deliveryId.toString())
      .set('forceUpdate', forceUpdate.toString());
    return this.http.post<CompareAndStockResponse>(`${this.baseUrl}/compare-and-stock`, {}, { params });
  }

  compareAndStockAllDeliveries(invoiceId: number, forceUpdate: boolean = false): Observable<BatchCompareAndStockResponse> {
    const params = new HttpParams()
      .set('invoiceId', invoiceId.toString())
      .set('forceUpdate', forceUpdate.toString());
    return this.http.post<BatchCompareAndStockResponse>(`${this.baseUrl}/compare-and-stock-all-deliveries`, {}, { params });
  }

  saveAdjustment(request: {
    deliveryId: number;
    invoiceId: number;
    documentLineId?: number | null;
    productKey: string;
    deliveryQuantity: number;
    actualQuantity?: number | null;
    validate: boolean;
  }): Observable<any> {
    return this.http.post(`${this.baseUrl}/adjustments`, request);
  }

  reparseLines(id: number, useAiFallback = false): Observable<{ documentId: number; success: boolean }> {
    const params = new HttpParams().set('useAiFallback', String(useAiFallback));
    return this.http.post<{ documentId: number; success: boolean }>(`${this.baseUrl}/${id}/reparse-lines`, null, { params });
  }

  inspect(file: File): Observable<{ typeDocument: string; numero?: string; client?: string; dateDocument?: string; supplier?: string; }> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<{ typeDocument: string; numero?: string; client?: string; dateDocument?: string; supplier?: string; }>(`${this.baseUrl}/inspect`, form);
  }

  findInvoicesByBlNumber(blNumber: string): Observable<Document[]> {
    const params = new HttpParams().set('blNumber', blNumber);
    return this.http.get<Document[]>(`${this.baseUrl}/find-invoices-by-bl-number`, { params });
  }
}


