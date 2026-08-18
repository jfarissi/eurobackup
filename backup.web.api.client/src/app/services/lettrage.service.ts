import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { LettrageAccountSummary, LettrageGroup, LettrageLine } from '../models/accounting';

/** Accès API Phase 3 : lettrage comptable des lignes d'écritures (api/lettrage). */
@Injectable({
  providedIn: 'root'
})
export class LettrageService {
  constructor(private http: HttpClient) {}

  /** Lignes non lettrées d'un compte (écritures Posted/Validated), triées par date. */
  getUnlettered(accountCode: string): Observable<LettrageLine[]> {
    const params = new HttpParams().set('accountCode', accountCode);
    return this.http.get<LettrageLine[]>('/api/lettrage/unlettered', { params });
  }

  /** Groupes de lettrage existants (tous comptes ou filtré par compte). */
  getGroups(accountCode?: string): Observable<LettrageGroup[]> {
    let params = new HttpParams();
    if (accountCode) params = params.set('accountCode', accountCode);
    return this.http.get<LettrageGroup[]>('/api/lettrage/groups', { params });
  }

  /** Lettrage automatique ; sans compte = comptes clients + fournisseurs des paramètres société. */
  automatic(accountCode?: string): Observable<LettrageAccountSummary[]> {
    return this.http.post<LettrageAccountSummary[]>('/api/lettrage/automatic', { accountCode: accountCode ?? null });
  }

  /** Lettrage manuel d'une sélection de lignes (même compte, équilibrée). Retourne le code LET-. */
  manual(lineIds: number[]): Observable<{ code: string }> {
    return this.http.post<{ code: string }>('/api/lettrage/manual', { lineIds });
  }

  /** Délettrage : efface le code des lignes du groupe. Retourne le nombre de lignes délettrées. */
  deletter(code: string): Observable<{ delettered: number }> {
    return this.http.delete<{ delettered: number }>(`/api/lettrage/${encodeURIComponent(code)}`);
  }
}
