import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { FixedAsset, FixedAssetForm, FixedAssetPostResult } from '../models/accounting';

/** Accès API immobilisations (api/fixed-assets). */
@Injectable({
  providedIn: 'root'
})
export class FixedAssetsApiService {
  constructor(private http: HttpClient) {}

  list(): Observable<FixedAsset[]> {
    return this.http.get<FixedAsset[]>('/api/fixed-assets');
  }

  get(id: number): Observable<FixedAsset> {
    return this.http.get<FixedAsset>(`/api/fixed-assets/${id}`);
  }

  create(form: FixedAssetForm): Observable<FixedAsset> {
    return this.http.post<FixedAsset>('/api/fixed-assets', form);
  }

  update(id: number, form: FixedAssetForm): Observable<FixedAsset> {
    return this.http.put<FixedAsset>(`/api/fixed-assets/${id}`, form);
  }

  recalculate(id: number): Observable<FixedAsset> {
    return this.http.post<FixedAsset>(`/api/fixed-assets/${id}/recalculate`, {});
  }

  deactivate(id: number): Observable<FixedAsset> {
    return this.http.post<FixedAsset>(`/api/fixed-assets/${id}/deactivate`, {});
  }

  postMonth(year: number, month: number): Observable<FixedAssetPostResult> {
    const params = new HttpParams().set('year', year).set('month', month);
    return this.http.post<FixedAssetPostResult>('/api/fixed-assets/post-month', {}, { params });
  }
}
