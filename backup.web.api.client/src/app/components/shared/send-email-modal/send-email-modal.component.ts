import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MaterialModule } from '../../../material.module';
import { EmailService, EmailPreview, EmailHistoryItem } from '../../../services/email.service';
import { AppI18nService } from '../../../services/app-i18n.service';
import { TPipe } from '../../../pipes/t.pipe';
import { MatSnackBar } from '@angular/material/snack-bar';

@Component({
  selector: 'app-send-email-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, TPipe],
  templateUrl: './send-email-modal.component.html',
  styleUrls: ['./send-email-modal.component.css']
})
export class SendEmailModalComponent implements OnChanges {
  @Input() open = false;
  @Input() documentType = '';
  @Input() documentId = 0;
  @Input() templateCode = '';
  @Output() closed = new EventEmitter<void>();
  @Output() sent = new EventEmitter<void>();

  loading = false;
  sending = false;
  error = '';
  preview: EmailPreview | null = null;
  history: EmailHistoryItem[] = [];
  toEmail = '';
  ccEmails = '';
  subject = '';
  bodyHtml = '';
  editHtml = false;

  constructor(
    private emailService: EmailService,
    private snack: MatSnackBar,
    private i18n: AppI18nService
  ) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['open']?.currentValue && this.documentType && this.documentId > 0) {
      this.loadPreview();
      this.loadHistory();
    }
  }

  loadPreview(): void {
    this.loading = true;
    this.error = '';
    this.emailService.preview(this.documentType, this.documentId, this.templateCode || undefined).subscribe({
      next: (p: EmailPreview) => {
        this.preview = p;
        this.toEmail = p.toEmail || '';
        this.subject = p.subject;
        this.bodyHtml = p.bodyHtml;
        this.editHtml = false;
        this.loading = false;
      },
      error: (err: { error?: { error?: string } }) => {
        this.error = err?.error?.error || this.i18n.t('email.previewError');
        this.loading = false;
      }
    });
  }

  loadHistory(): void {
    this.emailService.getHistory(this.documentType, this.documentId).subscribe({
      next: (items) => this.history = items || [],
      error: () => this.history = []
    });
  }

  send(): void {
    if (!this.toEmail?.trim()) {
      this.error = this.i18n.t('email.toRequired');
      return;
    }

    const previous = this.lastSuccessfulSend();
    if (previous) {
      const when = previous.sentAt || previous.createdAt;
      const whenLabel = when
        ? new Date(when).toLocaleString(this.i18n.numberLocale())
        : '—';
      const ok = confirm(this.i18n.t('email.resendConfirm', {
        to: previous.toEmail || '—',
        when: whenLabel,
        template: previous.templateCode || ''
      }));
      if (!ok) return;
    }

    this.doSend();
  }

  /** Dernier envoi réussi pour ce document (éventuellement même modèle). */
  lastSuccessfulSend(): EmailHistoryItem | null {
    const sent = this.history.filter(h => (h.status || '').toLowerCase() === 'sent');
    if (sent.length === 0) return null;
    if (this.templateCode) {
      const sameTpl = sent.find(h =>
        (h.templateCode || '').toLowerCase() === this.templateCode.toLowerCase());
      if (sameTpl) return sameTpl;
    }
    return sent[0];
  }

  get alreadySentHint(): string {
    const previous = this.lastSuccessfulSend();
    if (!previous) return '';
    const when = previous.sentAt || previous.createdAt;
    const whenLabel = when
      ? new Date(when).toLocaleString(this.i18n.numberLocale())
      : '—';
    return this.i18n.t('email.alreadySentHint', {
      to: previous.toEmail || '—',
      when: whenLabel
    });
  }

  private doSend(): void {
    this.sending = true;
    this.error = '';
    this.emailService.send({
      documentType: this.documentType,
      documentId: this.documentId,
      templateCode: this.templateCode || undefined,
      toEmail: this.toEmail.trim(),
      ccEmails: this.ccEmails.trim() || undefined,
      subject: this.subject,
      bodyHtml: this.bodyHtml,
      sendNow: true
    }).subscribe({
      next: () => {
        this.sending = false;
        this.snack.open(this.i18n.t('email.sentSuccess'), this.i18n.t('common.close'), { duration: 3000 });
        this.sent.emit();
        this.loadHistory();
        this.close();
      },
      error: (err: { error?: { error?: string } | string }) => {
        this.sending = false;
        const body = err?.error;
        this.error = (typeof body === 'string' ? body : body?.error) || this.i18n.t('email.sendError');
        this.loadHistory();
      }
    });
  }

  close(): void {
    this.open = false;
    this.preview = null;
    this.history = [];
    this.error = '';
    this.closed.emit();
  }
}
