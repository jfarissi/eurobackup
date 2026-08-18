import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { BankReconciliation } from '../models/accounting';

export interface OcrInvoiceLine {
  product: string;
  quantity: number;
  unitPrice: number;
}

export interface OcrInvoice {
  documentType: string;
  ice?: string | null;
  taxId?: string | null;
  tradeRegister?: string | null;
  invoiceNumber?: string | null;
  invoiceDate?: string | null;
  partyName?: string | null;
  amountHt?: number | null;
  vatAmount?: number | null;
  amountTtc?: number | null;
  vatRate?: number | null;
  confidence: number;
  lineCount?: number;
  lines?: OcrInvoiceLine[];
  source?: 'header' | 'purchaseParser';
}

export interface OcrInvoiceImport {
  invoiceId: number;
  invoiceNumber: string;
  supplierId: number;
  supplierName: string;
  created: boolean;
  lineCount: number;
  source: string;
  extraction: OcrInvoice;
}

export interface OcrBankLine {
  operationDate: string;
  label: string;
  reference?: string | null;
  debit: number;
  credit: number;
}

export interface OcrUnifiedExtract {
  documentType: string;
  typeConfidence: number;
  source?: string;
  invoice?: OcrInvoice | null;
  bankLines?: OcrBankLine[];
}

@Injectable({ providedIn: 'root' })
export class AccountingOcrApiService {
  constructor(private http: HttpClient) {}

    extract(text: string, fileName?: string, hint?: string): Observable<OcrUnifiedExtract> {
      return this.http.post<OcrUnifiedExtract>('/api/accounting-ocr/extract', { text, fileName, hint });
    }

    extractFile(file: File, hint?: string): Observable<OcrUnifiedExtract> {
      const body = new FormData();
      body.append('file', file, file.name);
      if (hint) body.append('hint', hint);
      return this.http.post<OcrUnifiedExtract>('/api/accounting-ocr/extract/file', body);
    }

    invoice(text: string): Observable<OcrInvoice> {
      return this.http.post<OcrInvoice>('/api/accounting-ocr/invoice', { text });
    }

    invoiceFile(file: File): Observable<OcrInvoice> {
      const body = new FormData();
      body.append('file', file, file.name);
      return this.http.post<OcrInvoice>('/api/accounting-ocr/invoice/file', body);
    }

    bankStatement(text: string, bank?: string, fileName?: string) {
      return this.http.post<{ bank?: string | null; lines: OcrBankLine[] }>(
        '/api/accounting-ocr/bank-statement',
        { text, bank, fileName });
    }

    bankStatementFile(file: File, bank?: string) {
      const body = new FormData();
      body.append('file', file, file.name);
      if (bank) body.append('bank', bank);
      return this.http.post<{ bank?: string | null; lines: OcrBankLine[] }>(
        '/api/accounting-ocr/bank-statement/file', body);
    }

    importBankStatement(text: string, accountCode?: string, fileName?: string): Observable<BankReconciliation> {
      return this.http.post<BankReconciliation>('/api/accounting-ocr/bank-statement/import', {
        text, accountCode, fileName
      });
    }

    importBankStatementFile(file: File, accountCode?: string): Observable<BankReconciliation> {
      const body = new FormData();
      body.append('file', file, file.name);
      if (accountCode) body.append('accountCode', accountCode);
      return this.http.post<BankReconciliation>('/api/accounting-ocr/bank-statement/import/file', body);
    }

    importInvoice(text: string): Observable<OcrInvoiceImport> {
      return this.http.post<OcrInvoiceImport>('/api/accounting-ocr/invoice/import', { text });
    }

    importInvoiceFile(file: File): Observable<OcrInvoiceImport> {
      const body = new FormData();
      body.append('file', file, file.name);
      return this.http.post<OcrInvoiceImport>('/api/accounting-ocr/invoice/import/file', body);
    }
}
