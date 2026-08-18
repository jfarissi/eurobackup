import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface CabinetDossier {
  companyId: string;
  name: string;
  missionLevel: string;
  isActive: boolean;
  currentPeriod?: string | null;
  closingStatus: string;
  unresolvedAnnotations: number;
}

export interface CabinetCompanyOption {
  id: string;
  name: string;
}

export interface CabinetEntry {
  id: number;
  entryDate: string;
  entryNumber: string;
  description: string;
  status: string;
  debit: number;
  credit: number;
  annotationCount: number;
}

export interface CabinetAnnotation {
  id: number;
  accountingEntryId?: number | null;
  type: string;
  message: string;
  author?: string | null;
  isResolved: boolean;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class CabinetApiService {
  constructor(private http: HttpClient) {}

  dossiers(): Observable<CabinetDossier[]> {
    return this.http.get<CabinetDossier[]>('/api/cabinet/dossiers');
  }

  companies(): Observable<CabinetCompanyOption[]> {
    return this.http.get<CabinetCompanyOption[]>('/api/cabinet/companies');
  }

  link(clientCompanyId: string, missionLevel?: string): Observable<CabinetDossier> {
    return this.http.post<CabinetDossier>('/api/cabinet/dossiers', { clientCompanyId, missionLevel });
  }

  entries(companyId: string): Observable<CabinetEntry[]> {
    return this.http.get<CabinetEntry[]>(`/api/cabinet/dossiers/${companyId}/entries`);
  }

  annotations(companyId: string, entryId?: number): Observable<CabinetAnnotation[]> {
    let params = new HttpParams();
    if (entryId) params = params.set('entryId', entryId);
    return this.http.get<CabinetAnnotation[]>(`/api/cabinet/dossiers/${companyId}/annotations`, { params });
  }

  annotate(companyId: string, message: string, type: string, accountingEntryId?: number): Observable<CabinetAnnotation> {
    return this.http.post<CabinetAnnotation>(`/api/cabinet/dossiers/${companyId}/annotations`, {
      message, type, accountingEntryId
    });
  }

  resolve(id: number): Observable<CabinetAnnotation> {
    return this.http.post<CabinetAnnotation>(`/api/cabinet/annotations/${id}/resolve`, {});
  }

  validateClose(companyId: string, year: number, month: number, force: boolean) {
    return this.http.post<{ message: string }>(`/api/cabinet/dossiers/${companyId}/validate-close`, {
      year, month, force
    });
  }
}
