import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  ErpBrand,
  ErpCatalogSyncFilter,
  ErpCategory,
  ErpChangesPage,
  ErpChangesQuery,
  ErpProduct,
  ErpProductsPage,
  ErpProductsQuery,
  ErpProductSummary,
  ErpSyncLog,
  ErpSyncLogsPage,
  ExcelImportResult
} from '../models/erp-product';

@Injectable({ providedIn: 'root' })
export class ErpProductService {
  private baseUrl = `${environment.apiBaseUrl}/erp-products`;

  constructor(private http: HttpClient) {}

  getProducts(query: ErpProductsQuery = {}): Observable<ErpProductsPage> {
    let params = new HttpParams();
    if (query.page != null) params = params.set('page', String(query.page));
    if (query.pageSize != null) params = params.set('pageSize', String(query.pageSize));
    if (query.brand) params = params.set('brand', query.brand);
    if (query.q) params = params.set('q', query.q);
    if (query.fromExcel != null) params = params.set('fromExcel', String(query.fromExcel));
    if (query.dataSource) params = params.set('dataSource', query.dataSource);
    if (query.mainTypeId) params = params.set('mainTypeId', query.mainTypeId);
    if (query.typeId) params = params.set('typeId', query.typeId);
    if (query.subTypeId) params = params.set('subTypeId', query.subTypeId);
    return this.http.get<ErpProductsPage>(this.baseUrl, { params });
  }

  getById(id: number): Observable<ErpProduct> {
    return this.http.get<ErpProduct>(`${this.baseUrl}/${id}`);
  }

  getChanges(query: ErpChangesQuery = {}): Observable<ErpChangesPage> {
    let params = new HttpParams();
    if (query.unreadOnly != null) {
      params = params.set('unreadOnly', String(query.unreadOnly));
    }
    if (query.changeType) {
      params = params.set('changeType', query.changeType);
    }
    if (query.valueMode) {
      params = params.set('valueMode', query.valueMode);
    }
    if (query.q) {
      params = params.set('q', query.q);
    }
    if (query.from) {
      params = params.set('from', query.from);
    }
    if (query.to) {
      params = params.set('to', query.to);
    }
    if (query.page != null) {
      params = params.set('page', String(query.page));
    }
    if (query.pageSize != null) {
      params = params.set('pageSize', String(query.pageSize));
    }
    return this.http.get<ErpChangesPage>(`${this.baseUrl}/changes`, { params });
  }

  markChangesRead(ids: number[]): Observable<{ marked: number }> {
    return this.http.post<{ marked: number }>(`${this.baseUrl}/changes/mark-read`, { ids });
  }

  deleteChanges(ids: number[]): Observable<{ deleted: number }> {
    return this.http.post<{ deleted: number }>(`${this.baseUrl}/changes/delete`, { ids });
  }

  cleanupFormattingFalsePositives(): Observable<{ deleted: number }> {
    return this.http.post<{ deleted: number }>(`${this.baseUrl}/changes/cleanup-formatting`, {});
  }

  getSyncLogs(page = 1, pageSize = 10): Observable<ErpSyncLogsPage> {
    const params = new HttpParams()
      .set('page', String(page))
      .set('pageSize', String(pageSize));
    return this.http.get<ErpSyncLogsPage>(`${this.baseUrl}/sync-logs`, { params });
  }

  syncOne(erpId: string): Observable<ErpProductSummary> {
    return this.http.post<ErpProductSummary>(`${this.baseUrl}/sync/${encodeURIComponent(erpId)}`, {});
  }

  /** Sync un produit local via sa PK MySQL (résout ensuite l'ID ERP / EAN / réf). */
  syncProduct(product: { id: number; erpProductId: string }): Observable<ErpProduct> {
    return this.http.post<ErpProduct>(`${this.baseUrl}/${product.id}/sync`, {});
  }

  syncAll(): Observable<ErpSyncLog> {
    return this.http.post<ErpSyncLog>(`${this.baseUrl}/sync-all`, {});
  }

  getBrands(query: { mainTypeId?: string; typeId?: string; subTypeId?: string } = {}): Observable<ErpBrand[]> {
    let params = new HttpParams();
    if (query.mainTypeId) params = params.set('mainTypeId', query.mainTypeId);
    if (query.typeId) params = params.set('typeId', query.typeId);
    if (query.subTypeId) params = params.set('subTypeId', query.subTypeId);
    return this.http.get<ErpBrand[]>(`${this.baseUrl}/brands`, { params });
  }

  getCategories(query: {
    level?: string;
    parentId?: number;
    brand?: string;
    mainTypeId?: string;
    typeId?: string;
  } = {}): Observable<ErpCategory[]> {
    let params = new HttpParams();
    if (query.level) params = params.set('level', query.level);
    if (query.parentId != null) params = params.set('parentId', String(query.parentId));
    if (query.brand) params = params.set('brand', query.brand);
    if (query.mainTypeId) params = params.set('mainTypeId', query.mainTypeId);
    if (query.typeId) params = params.set('typeId', query.typeId);
    return this.http.get<ErpCategory[]>(`${this.baseUrl}/categories`, { params });
  }

  syncCatalog(filter: ErpCatalogSyncFilter, cancelPrevious = true): Observable<ErpSyncLog> {
    let params = new HttpParams().set('cancelPrevious', String(cancelPrevious));
    if (filter.mainTypeId) params = params.set('mainTypeId', filter.mainTypeId);
    if (filter.typeId) params = params.set('typeId', filter.typeId);
    if (filter.subTypeId) params = params.set('subTypeId', filter.subTypeId);
    if (filter.brand) params = params.set('brand', filter.brand);
    return this.http.post<ErpSyncLog>(`${this.baseUrl}/sync-catalog`, {}, { params });
  }

  cancelRunningSync(): Observable<ErpSyncLog> {
    return this.http.post<ErpSyncLog>(`${this.baseUrl}/sync-cancel`, {});
  }

  getSyncLog(jobId: string): Observable<ErpSyncLog> {
    return this.http.get<ErpSyncLog>(`${this.baseUrl}/sync-logs/${encodeURIComponent(jobId)}`);
  }

  importExcel(syncAfter = false): Observable<{ import: ExcelImportResult; sync?: ErpSyncLog | null }> {
    const params = new HttpParams().set('syncAfter', String(syncAfter));
    return this.http.post<{ import: ExcelImportResult; sync?: ErpSyncLog | null }>(
      `${this.baseUrl}/import-excel`,
      {},
      { params }
    );
  }
}
