import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface HelpContentDto {
  id?: number;
  helpKey: string;
  lang: string;
  title: string;
  n1?: string | null;
  body?: string | null;
  rules?: string | null;
  example?: string | null;
  guide?: string | null;
  version: string;
  status: string;
  validFrom?: string | null;
  validTo?: string | null;
  rgIds?: string | null;
  documentType?: string | null;
  fieldId?: string | null;
  updatedBy?: string | null;
  createdAt?: string;
  updatedAt?: string;
}

export interface HelpAnalyticsSummary {
  days: number;
  publishedCount: number;
  draftCount: number;
  pendingDownReports: number;
  byKey: Array<{
    helpKey: string;
    opens: number;
    up: number;
    down: number;
    usefulness: number | null;
  }>;
}

@Injectable({ providedIn: 'root' })
export class HelpApiService {
  private base = `${environment.apiBaseUrl}/help`;

  constructor(private http: HttpClient) {}

  getPublished(lang: string): Observable<HelpContentDto[]> {
    return this.http.get<HelpContentDto[]>(`${this.base}/published`, {
      params: new HttpParams().set('lang', lang)
    });
  }

  listAdmin(lang?: string, status?: string): Observable<HelpContentDto[]> {
    let params = new HttpParams();
    if (lang) params = params.set('lang', lang);
    if (status) params = params.set('status', status);
    return this.http.get<HelpContentDto[]>(`${this.base}/admin`, { params });
  }

  create(dto: HelpContentDto): Observable<HelpContentDto> {
    return this.http.post<HelpContentDto>(`${this.base}/admin`, dto);
  }

  update(id: number, dto: HelpContentDto): Observable<HelpContentDto> {
    return this.http.put<HelpContentDto>(`${this.base}/admin/${id}`, dto);
  }

  transition(id: number, status: string): Observable<HelpContentDto> {
    return this.http.post<HelpContentDto>(`${this.base}/admin/${id}/transition`, { status });
  }

  archive(id: number): Observable<{ ok: boolean }> {
    return this.http.delete<{ ok: boolean }>(`${this.base}/admin/${id}`);
  }

  sendFeedback(helpKey: string, vote: 'up' | 'down', reason?: string, comment?: string): Observable<{ ok: boolean }> {
    return this.http.post<{ ok: boolean }>(`${this.base}/feedback`, { helpKey, vote, reason, comment });
  }

  track(helpKey: string, action: string): Observable<{ ok: boolean }> {
    return this.http.post<{ ok: boolean }>(`${this.base}/analytics`, { helpKey, action });
  }

  analyticsSummary(days = 30): Observable<HelpAnalyticsSummary> {
    return this.http.get<HelpAnalyticsSummary>(`${this.base}/analytics/summary`, {
      params: new HttpParams().set('days', String(days))
    });
  }
}
