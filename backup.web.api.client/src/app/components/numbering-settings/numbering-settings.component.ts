import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MaterialModule } from '../../material.module';
import { BusinessService } from '../../services/business.service';
import { DocumentNumberSequence } from '../../models/business';
import { AppI18nService } from '../../services/app-i18n.service';
import { TPipe } from '../../pipes/t.pipe';

@Component({
  selector: 'app-numbering-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, TPipe],
  templateUrl: './numbering-settings.component.html',
  styleUrls: ['./numbering-settings.component.css']
})
export class NumberingSettingsComponent implements OnInit {
  sequences: DocumentNumberSequence[] = [];
  drafts: Record<number, DocumentNumberSequence> = {};
  previews: Record<number, string> = {};
  savingId: number | null = null;
  loading = false;
  actionMessage = '';
  actionError = '';

  readonly placeholderExamples = [
    '{Prefix}',
    '{Year}',
    '{Number:D4}',
    '{Number:D5}',
    '{Number}'
  ];
  readonly formatExample = 'FAC-{Year}-{Number:D4}';
  readonly formatExampleResult = 'FAC-2026-0001';

  private readonly typeLabelKeys: Record<string, string> = {
    Quote: 'numbering.type.Quote',
    Order: 'numbering.type.Order',
    Invoice: 'numbering.type.Invoice',
    CreditNote: 'numbering.type.CreditNote',
    PurchaseOrder: 'numbering.type.PurchaseOrder',
    SupplierInvoice: 'numbering.type.SupplierInvoice',
    DeliveryNote: 'numbering.type.DeliveryNote'
  };

  constructor(private businessService: BusinessService, private i18n: AppI18nService) {}

  ngOnInit(): void {
    this.loadSequences();
  }

  loadSequences(): void {
    this.loading = true;
    this.actionError = '';
    this.businessService.getNumberingSequences().subscribe({
      next: (sequences) => {
        this.applySequences(sequences);
        this.loading = false;
        if (sequences.length === 0) {
          this.ensureDefaults(false);
        }
      },
      error: (error) => {
        this.loading = false;
        this.actionError = error?.error?.error || error?.error || this.i18n.t('numbering.loadError');
      }
    });
  }

  ensureDefaults(showMessage = true): void {
    this.actionError = '';
    this.businessService.ensureNumberingDefaults().subscribe({
      next: (sequences) => {
        this.applySequences(sequences);
        if (showMessage) {
          this.actionMessage = this.i18n.t('numbering.defaultsReady');
        }
      },
      error: (error) => {
        this.actionError = error?.error?.error || error?.error || this.i18n.t('numbering.initError');
      }
    });
  }

  refreshPreview(seq: DocumentNumberSequence): void {
    if (!seq.id) return;
    const draft = this.drafts[seq.id];
    if (!draft) return;

    // Local preview from draft fields (no API consume)
    this.previews[seq.id] = this.formatPreview(draft);
  }

  save(seq: DocumentNumberSequence): void {
    if (!seq.id) return;
    const draft = this.drafts[seq.id];
    if (!draft) return;

    if (!draft.prefix?.trim()) {
      this.actionError = this.i18n.t('numbering.prefixRequired');
      return;
    }
    if (!draft.nextNumber || draft.nextNumber < 1) {
      this.actionError = this.i18n.t('numbering.nextNumberInvalid');
      return;
    }
    if (!draft.formatPattern?.trim()) {
      this.actionError = this.i18n.t('numbering.formatRequired');
      return;
    }

    this.savingId = seq.id;
    this.actionError = '';
    this.businessService.updateNumberingSequence(seq.id, {
      ...draft,
      prefix: draft.prefix.trim(),
      formatPattern: draft.formatPattern.trim()
    }).subscribe({
      next: (updated) => {
        this.savingId = null;
        this.actionMessage = this.i18n.t('numbering.saved', { type: this.typeLabel(updated.documentType) });
        this.loadSequences();
      },
      error: (error) => {
        this.savingId = null;
        this.actionError = error?.error?.error || error?.error || this.i18n.t('numbering.saveError');
      }
    });
  }

  typeLabel(documentType: string): string {
    const key = this.typeLabelKeys[documentType];
    return key ? this.i18n.t(key) : documentType;
  }

  private applySequences(sequences: DocumentNumberSequence[]): void {
    this.sequences = [...(sequences || [])].sort((a, b) =>
      this.typeLabel(a.documentType).localeCompare(this.typeLabel(b.documentType), 'fr')
    );
    this.drafts = {};
    this.previews = {};
    for (const seq of this.sequences) {
      if (!seq.id) continue;
      this.drafts[seq.id] = { ...seq };
      this.previews[seq.id] = this.formatPreview(seq);
    }
  }

  private formatPreview(seq: DocumentNumberSequence): string {
    const year = seq.year || new Date().getFullYear();
    const number = seq.nextNumber || 1;
    return (seq.formatPattern || '{Prefix}{Year}-{Number:D4}')
      .replace('{Prefix}', seq.prefix || '')
      .replace('{Year}', String(year))
      .replace('{Number:D4}', String(number).padStart(4, '0'))
      .replace('{Number:D5}', String(number).padStart(5, '0'))
      .replace('{Number}', String(number));
  }
}
