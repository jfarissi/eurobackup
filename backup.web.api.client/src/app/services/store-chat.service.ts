import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  StoreChatMessageRequest,
  StoreChatPaymentResult,
  StoreChatResponse
} from '../models/store-chat';

@Injectable({ providedIn: 'root' })
export class StoreChatService {
  private readonly baseUrl = `${environment.apiBaseUrl}/store-chat`;
  private readonly sessionKey = 'store_chat_session_id';

  constructor(private http: HttpClient) {}

  getSessionId(): string | null {
    return localStorage.getItem(this.sessionKey);
  }

  setSessionId(sessionId: string): void {
    if (sessionId) {
      localStorage.setItem(this.sessionKey, sessionId);
    }
  }

  clearSessionId(): void {
    localStorage.removeItem(this.sessionKey);
  }

  sendMessage(message: StoreChatMessageRequest): Observable<StoreChatResponse> {
    const sessionId = message.sessionId || this.getSessionId() || undefined;
    const headers = sessionId
      ? new HttpHeaders({ 'X-Store-Chat-Session': sessionId })
      : undefined;

    const returnBaseUrl = message.returnBaseUrl || (typeof window !== 'undefined' ? window.location.origin : undefined);

    return this.http
      .post<StoreChatResponse>(
        `${this.baseUrl}/message`,
        { ...message, sessionId, returnBaseUrl },
        { headers }
      )
      .pipe(tap(res => {
        if (res?.sessionId) this.setSessionId(res.sessionId);
      }));
  }

  getPaymentResult(orderId: string): Observable<StoreChatPaymentResult> {
    return this.http.get<StoreChatPaymentResult>(`${this.baseUrl}/payment-result/${orderId}`);
  }

  confirmPayment(orderId: string, sessionId?: string | null): Observable<StoreChatPaymentResult> {
    return this.http.post<StoreChatPaymentResult>(`${this.baseUrl}/confirm-payment`, {
      orderId,
      sessionId
    });
  }
}
