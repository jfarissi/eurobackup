import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MaterialModule } from '../../../material.module';
import { AccountingChartService } from '../../../services/accounting-chart.service';
import { AccountingExportPreview, AccountingExportsApiService } from '../../../services/accounting-exports.service';
import { FiscalYear } from '../../../models/accounting';
import { AppI18nService } from '../../../services/app-i18n.service';
import { TPipe } from '../../../pipes/t.pipe';
import { downloadBlob, fileNameFromContentDisposition } from '../../../utils/download-blob.util';
import { FormHelpComponent } from '../../shared/form-help/form-help.component';

@Component({
  selector: 'app-accounting-exports',
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, TPipe, FormHelpComponent],
  templateUrl: './accounting-exports.component.html',
  styleUrls: ['./accounting-exports.component.css']
})
export class AccountingExportsComponent implements OnInit {
  years: FiscalYear[] = [];
  selectedId: number | null = null;
  preview: AccountingExportPreview | null = null;

  loading = false;
  downloading: 'fec' | 'csv' | null = null;
  actionMessage = '';
  actionError = '';

  constructor(
    private chart: AccountingChartService,
    private api: AccountingExportsApiService,
    private i18n: AppI18nService
  ) {}

  ngOnInit(): void {
    this.chart.getFiscalYears().subscribe({
      next: (years) => {
        this.years = years || [];
        const open = this.years.find(y => y.status === 'Open') ?? this.years[0];
        if (open) {
          this.selectedId = open.id;
          this.loadPreview();
        }
      },
      error: (err) => {
        this.actionError = this.errorText(err) || this.i18n.t('exports.loadError');
      }
    });
  }

  loadPreview(): void {
    if (!this.selectedId) {
      this.preview = null;
      return;
    }
    this.loading = true;
    this.actionError = '';
    this.api.preview(this.selectedId).subscribe({
      next: (dto) => {
        this.preview = dto;
        this.loading = false;
      },
      error: (err) => {
        this.preview = null;
        this.loading = false;
        this.actionError = this.errorText(err) || this.i18n.t('exports.loadError');
      }
    });
  }

  download(kind: 'fec' | 'csv'): void {
    if (!this.selectedId || this.downloading) return;
    this.downloading = kind;
    this.actionMessage = '';
    this.actionError = '';
    const request = kind === 'fec' ? this.api.downloadFec(this.selectedId) : this.api.downloadCsv(this.selectedId);
    const fallback = kind === 'fec' ? 'FEC.txt' : 'ECRITURES.csv';
    request.subscribe({
      next: (response) => {
        const blob = response.body;
        if (blob) {
          const name = fileNameFromContentDisposition(response.headers.get('Content-Disposition'), fallback);
          downloadBlob(blob, name);
          this.actionMessage = this.i18n.t('exports.downloaded', { fileName: name });
        }
        this.downloading = null;
      },
      error: (err) => {
        this.downloading = null;
        void this.readBlobError(err).then(text => {
          this.actionError = text || this.i18n.t('exports.actionError');
        });
      }
    });
  }

  private async readBlobError(err: unknown): Promise<string> {
    const e = err as { error?: unknown };
    if (e?.error instanceof Blob) {
      try { return (await e.error.text()).trim(); } catch { return ''; }
    }
    return this.errorText(err);
  }

  private errorText(err: unknown): string {
    const e = err as { error?: unknown };
    if (typeof e?.error === 'string') return e.error;
    const obj = e?.error as { error?: string; message?: string } | undefined;
    return obj?.error || obj?.message || '';
  }
}
