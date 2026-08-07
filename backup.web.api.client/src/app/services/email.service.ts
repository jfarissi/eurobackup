import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface EmailPreview {
  templateCode: string;
  documentType: string;
  documentId: number;
  documentNumber: string;
  toEmail?: string;
  recipientName?: string;
  subject: string;
  bodyHtml: string;
  attachmentFileName?: string;
  attachmentSize?: number;
  hasValidRecipient: boolean;
}

export interface SendEmailRequest {
  documentType: string;
  documentId: number;
  templateCode?: string;
  toEmail?: string;
  ccEmails?: string;
  replyTo?: string;
  subject?: string;
  bodyHtml?: string;
  scheduledAt?: string;
  sendNow?: boolean;
}

export interface EmailHistoryItem {
  id: number;
  trackingId: string;
  templateCode: string;
  documentType?: string;
  documentId?: number;
  documentNumber?: string;
  toEmail: string;
  ccEmails?: string;
  subject: string;
  status: string;
  scheduledAt?: string;
  sentAt?: string;
  lastError?: string;
  createdBy: string;
  createdAt: string;
  hasAttachment: boolean;
}

export interface CompanyEmailSettings {
  companyId: string;
  enabled: boolean;
  smtpHost: string;
  smtpPort: number;
  useSsl: boolean;
  ignoreSslErrors?: boolean;
  username?: string;
  password?: string;
  fromEmail: string;
  fromDisplayName: string;
  defaultReplyTo?: string;
  maxEmailsPerHour: number;
  maxAttachmentBytes: number;
  footerHtml?: string;
  autoPaymentRemindersEnabled?: boolean;
  paymentReminderDaysN1?: number;
  paymentReminderDaysN2?: number;
  paymentReminderDaysN3?: number;
  autoStockAlertsEnabled?: boolean;
  stockAlertRecipients?: string;
  stockAlertCooldownHours?: number;
  autoEmailOnPurchaseOrderSend?: boolean;
}

@Injectable({ providedIn: 'root' })
export class EmailService {
  constructor(private http: HttpClient) {}

  preview(documentType: string, documentId: number, templateCode?: string): Observable<EmailPreview> {
    let params = new HttpParams().set('documentType', documentType).set('documentId', documentId);
    if (templateCode) params = params.set('templateCode', templateCode);
    return this.http.get<EmailPreview>('/api/emails/preview', { params });
  }

  send(request: SendEmailRequest): Observable<unknown> {
    return this.http.post('/api/emails/send', { ...request, sendNow: request.sendNow !== false });
  }

  getHistory(documentType?: string, documentId?: number): Observable<EmailHistoryItem[]> {
    let params = new HttpParams();
    if (documentType) params = params.set('documentType', documentType);
    if (documentId) params = params.set('documentId', String(documentId));
    return this.http.get<EmailHistoryItem[]>('/api/emails', { params });
  }

  getSettings(): Observable<CompanyEmailSettings> {
    return this.http.get<CompanyEmailSettings>('/api/email-settings');
  }

  saveSettings(settings: CompanyEmailSettings): Observable<CompanyEmailSettings> {
    return this.http.put<CompanyEmailSettings>('/api/email-settings', settings);
  }

  testConnection(settings?: CompanyEmailSettings): Observable<{ success: boolean }> {
    return this.http.post<{ success: boolean }>('/api/email-settings/test', settings || {});
  }

  runPaymentReminders(): Observable<{ queued: number; skipped: number; messages: string[] }> {
    return this.http.post<{ queued: number; skipped: number; messages: string[] }>('/api/emails/reminders/run', {});
  }

  runStockAlerts(): Observable<{ queued: number; skipped: number; messages: string[] }> {
    return this.http.post<{ queued: number; skipped: number; messages: string[] }>('/api/emails/stock-alerts/run', {});
  }
}
