import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { MaterialModule } from '../../../material.module';
import { AccountingOcrApiService, OcrBankLine, OcrInvoice, OcrInvoiceImport, OcrUnifiedExtract } from '../../../services/accounting-ocr.service';
import { PermissionService } from '../../../services/permission.service';
import { Permissions } from '../../../constants/permissions';
import { AppI18nService } from '../../../services/app-i18n.service';
import { TPipe } from '../../../pipes/t.pipe';
import { FormHelpComponent } from '../../shared/form-help/form-help.component';
import { FieldHelpComponent } from '../../shared/field-help/field-help.component';

const IMAGE_OR_PDF = /\.(png|jpe?g|webp|tiff?|bmp|gif|pdf)$/i;

@Component({
  selector: 'app-accounting-ocr',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, MaterialModule, TPipe, FormHelpComponent, FieldHelpComponent],
  templateUrl: './accounting-ocr.component.html',
  styleUrls: ['./accounting-ocr.component.css']
})
export class AccountingOcrComponent {
  text = '';
  fileName = '';
  pendingFile: File | null = null;
  mode: 'auto' | 'invoice' | 'bank' = 'auto';
  detectedType = '';
  typeConfidence = 0;
  extractSource = '';
  invoice: OcrInvoice | null = null;
  bankLines: OcrBankLine[] = [];
  importedInvoiceId: number | null = null;
  acting = false;
  actionMessage = '';
  actionError = '';

  constructor(
    private api: AccountingOcrApiService,
    public perm: PermissionService,
    private i18n: AppI18nService,
    private router: Router
  ) {}

  get canImport(): boolean {
    return this.perm.has(Permissions.AccountingCreate);
  }

  get confidencePct(): number {
    return Math.round((this.invoice?.confidence ?? 0) * 100);
  }

  get typeConfidencePct(): number {
    return Math.round((this.typeConfidence || 0) * 100);
  }

  get isBank(): boolean {
    return this.detectedType === 'releve_bancaire';
  }

  get isDelivery(): boolean {
    return this.detectedType === 'bon_livraison';
  }

  get isInvoice(): boolean {
    return this.detectedType === 'facture' || this.detectedType === 'invoice';
  }

  get detectedTypeLabel(): string {
    const key = 'ocr.detected.' + this.detectedType;
    const translated = this.i18n.t(key);
    return translated === key ? this.detectedType : translated;
  }

  get canExtract(): boolean {
    return !!this.pendingFile || !!this.text.trim();
  }

  get hasPurchaseLines(): boolean {
    return (this.invoice?.lineCount ?? 0) > 0;
  }

  onFile(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.fileName = file.name;
    this.importedInvoiceId = null;
    this.detectedType = '';
    this.invoice = null;
    this.bankLines = [];
    if (IMAGE_OR_PDF.test(file.name)) {
      this.pendingFile = file;
      this.text = '';
      return;
    }
    this.pendingFile = null;
    const reader = new FileReader();
    reader.onload = () => { this.text = String(reader.result || ''); };
    reader.readAsText(file);
  }

  extract(): void {
    if (!this.canExtract) return;
    this.acting = true;
    this.actionError = '';
    this.actionMessage = '';
    this.importedInvoiceId = null;
    const hint = this.mode === 'auto' ? undefined : this.mode === 'bank' ? 'releve_bancaire' : 'facture';
    const request$ = this.pendingFile
      ? this.api.extractFile(this.pendingFile, hint)
      : this.api.extract(this.text, this.fileName, hint);
    request$.subscribe({
      next: (dto: OcrUnifiedExtract) => {
        this.detectedType = (dto.documentType || '').toLowerCase();
        this.typeConfidence = dto.typeConfidence || 0;
        this.extractSource = dto.source || '';
        this.invoice = dto.invoice || null;
        this.bankLines = dto.bankLines || [];
        this.acting = false;
      },
      error: (err: unknown) => this.fail(err)
    });
  }

  importInvoice(): void {
    if (!this.canImport || !this.canExtract) return;
    this.acting = true;
    this.actionError = '';
    const request$ = this.pendingFile
      ? this.api.importInvoiceFile(this.pendingFile)
      : this.api.importInvoice(this.text);
    request$.subscribe({
      next: (dto: OcrInvoiceImport) => {
        this.acting = false;
        this.importedInvoiceId = dto.invoiceId;
        this.invoice = dto.extraction || this.invoice;
        this.actionMessage = this.i18n.t(dto.created ? 'ocr.invoiceCreated' : 'ocr.invoiceExists', {
          number: dto.invoiceNumber,
          supplier: dto.supplierName
        });
      },
      error: (err: unknown) => this.fail(err)
    });
  }

  openPurchases(): void {
    if (!this.importedInvoiceId) return;
    void this.router.navigate(['/purchases'], {
      queryParams: { supplierInvoiceId: this.importedInvoiceId, autoCreated: '1' }
    });
  }

  importBank(): void {
    if (!this.canImport || !this.canExtract) return;
    this.acting = true;
    this.actionError = '';
    const request$ = this.pendingFile
      ? this.api.importBankStatementFile(this.pendingFile)
      : this.api.importBankStatement(this.text, undefined, this.fileName);
    request$.subscribe({
      next: () => {
        this.acting = false;
        this.actionMessage = this.i18n.t('ocr.imported');
        void this.router.navigate(['/accounting/bank-reconciliation']);
      },
      error: (err: unknown) => this.fail(err)
    });
  }

  private fail(err: unknown): void {
    this.acting = false;
    this.actionError = this.errorText(err) || this.i18n.t('ocr.actionError');
  }

  private errorText(err: unknown): string {
    const e = err as { error?: unknown };
    if (typeof e?.error === 'string') return e.error;
    const obj = e?.error as { error?: string; message?: string } | undefined;
    return obj?.error || obj?.message || '';
  }
}
