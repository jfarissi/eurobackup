import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MaterialModule } from '../../../material.module';
import { AccountingChartService } from '../../../services/accounting-chart.service';
import { FiscalClosingService, ClosingPreview } from '../../../services/fiscal-closing.service';
import { FiscalYear } from '../../../models/accounting';
import { PermissionService } from '../../../services/permission.service';
import { Permissions } from '../../../constants/permissions';
import { AppI18nService } from '../../../services/app-i18n.service';
import { TPipe } from '../../../pipes/t.pipe';
import { FormHelpComponent } from '../../shared/form-help/form-help.component';

@Component({
  selector: 'app-fiscal-closing',
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, TPipe, FormHelpComponent],
  templateUrl: './fiscal-closing.component.html',
  styleUrls: ['./fiscal-closing.component.css']
})
export class FiscalClosingComponent implements OnInit {
  readonly P = Permissions;
  years: FiscalYear[] = [];
  selectedId: number | null = null;
  preview: ClosingPreview | null = null;
  loading = false;
  acting = false;
  actionMessage = '';
  actionError = '';

  constructor(
    private chart: AccountingChartService,
    private closing: FiscalClosingService,
    public perm: PermissionService,
    private i18n: AppI18nService
  ) {}

  get canManage(): boolean {
    return this.perm.has(Permissions.AccountingManageFiscalYears);
  }

  get selectedYear(): FiscalYear | undefined {
    return this.years.find(y => y.id === this.selectedId);
  }

  ngOnInit(): void {
    this.loadYears();
  }

  loadYears(): void {
    this.loading = true;
    this.actionError = '';
    this.chart.getFiscalYears().subscribe({
      next: (years) => {
        this.years = years;
        if (this.selectedId == null) {
          const open = years.find(y => y.status === 'Open') ?? years[0];
          this.selectedId = open?.id ?? null;
        }
        this.loading = false;
        if (this.selectedId) this.loadPreview();
      },
      error: (err) => {
        this.loading = false;
        this.actionError = this.errorText(err) || this.i18n.t('closing.loadError');
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
    this.closing.preview(this.selectedId).subscribe({
      next: (preview) => {
        this.preview = preview;
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        this.actionError = this.errorText(err) || this.i18n.t('closing.loadError');
      }
    });
  }

  closePeriod(periodId: number): void {
    if (!this.canManage) return;
    if (!confirm(this.i18n.t('closing.confirmPeriod'))) return;
    this.acting = true;
    this.actionMessage = '';
    this.actionError = '';
    this.closing.closePeriod(periodId).subscribe({
      next: () => {
        this.acting = false;
        this.actionMessage = this.i18n.t('closing.periodClosed');
        this.loadYears();
      },
      error: (err) => {
        this.acting = false;
        this.actionError = this.errorText(err) || this.i18n.t('closing.actionError');
      }
    });
  }

  closeYear(): void {
    if (!this.canManage || !this.selectedId || !this.preview?.canClose) return;
    if (!confirm(this.i18n.t('closing.confirmYear', { name: this.preview.yearName }))) return;
    this.acting = true;
    this.actionMessage = '';
    this.actionError = '';
    this.closing.closeYear(this.selectedId).subscribe({
      next: (res) => {
        this.acting = false;
        this.actionMessage = this.i18n.t('closing.yearClosed', {
          od: res.closeEntryNumber || '—',
          an: res.carryForwardEntryNumber || '—'
        });
        this.loadYears();
      },
      error: (err) => {
        this.acting = false;
        this.actionError = this.errorText(err) || this.i18n.t('closing.actionError');
      }
    });
  }

  openNext(): void {
    if (!this.canManage || !this.selectedId) return;
    this.acting = true;
    this.actionMessage = '';
    this.actionError = '';
    this.closing.openNext(this.selectedId).subscribe({
      next: (year) => {
        this.acting = false;
        this.actionMessage = this.i18n.t('closing.nextOpened', { name: year.name });
        this.loadYears();
      },
      error: (err) => {
        this.acting = false;
        this.actionError = this.errorText(err) || this.i18n.t('closing.actionError');
      }
    });
  }

  periodLabel(year: number, month: number): string {
    return `${this.i18n.t('vat.month.' + month)} ${year}`;
  }

  private errorText(err: unknown): string {
    const e = err as { error?: unknown };
    if (typeof e?.error === 'string') return e.error;
    const obj = e?.error as { error?: string; message?: string } | undefined;
    if (obj?.error || obj?.message) return obj.error || obj.message || '';
    const dto = e?.error as { error?: string } | undefined;
    return dto?.error || '';
  }
}
