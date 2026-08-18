import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MaterialModule } from '../../../material.module';
import { AccountingChartService } from '../../../services/accounting-chart.service';
import { FiscalPeriod, FiscalYear } from '../../../models/accounting';
import { PermissionService } from '../../../services/permission.service';
import { Permissions } from '../../../constants/permissions';
import { AppI18nService } from '../../../services/app-i18n.service';
import { TPipe } from '../../../pipes/t.pipe';
import { FormHelpComponent } from '../../shared/form-help/form-help.component';
import { FieldHelpComponent } from '../../shared/field-help/field-help.component';

@Component({
  selector: 'app-fiscal-years',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MaterialModule, TPipe, FormHelpComponent, FieldHelpComponent],
  templateUrl: './fiscal-years.component.html',
  styleUrls: ['./fiscal-years.component.css']
})
export class FiscalYearsComponent implements OnInit {
  readonly P = Permissions;

  years: FiscalYear[] = [];
  selected: FiscalYear | null = null;
  loading = false;
  saving = false;
  togglingPeriodId: number | null = null;
  actionMessage = '';
  actionError = '';
  modalError = '';

  showModal = false;
  form: FormGroup;

  constructor(
    private accountingChart: AccountingChartService,
    public perm: PermissionService,
    private i18n: AppI18nService,
    private fb: FormBuilder
  ) {
    this.form = this.fb.group({
      startDate: ['', Validators.required],
      endDate: ['', Validators.required],
      name: ['']
    });
  }

  get canManage(): boolean {
    return this.perm.has(Permissions.AccountingManageFiscalYears);
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.actionError = '';
    this.accountingChart.getFiscalYears().subscribe({
      next: (years) => {
        this.years = years || [];
        if (this.selected?.id) {
          this.selected = this.years.find(y => y.id === this.selected!.id) || null;
        }
        if (!this.selected && this.years.length) {
          this.selected = this.years[0];
        }
        this.loading = false;
      },
      error: (err) => {
        this.years = [];
        this.selected = null;
        this.loading = false;
        this.actionError = this.errorText(err) || this.i18n.t('accounting.fiscal.loadError');
      }
    });
  }

  select(year: FiscalYear): void {
    this.selected = year;
  }

  periodLabel(p: FiscalPeriod): string {
    return `${String(p.month).padStart(2, '0')}/${p.year}`;
  }

  openModal(): void {
    this.modalError = '';
    this.form.reset({ startDate: '', endDate: '', name: '' });
    this.showModal = true;
  }

  closeModal(): void {
    if (this.saving) return;
    this.showModal = false;
  }

  openYear(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.value;
    this.saving = true;
    this.modalError = '';
    this.accountingChart.openFiscalYear({
      startDate: v.startDate,
      endDate: v.endDate,
      name: (v.name || '').trim() || null
    }).subscribe({
      next: (created) => {
        this.saving = false;
        this.showModal = false;
        this.actionMessage = this.i18n.t('accounting.fiscal.opened', { name: created.name });
        this.load();
        this.selected = created;
      },
      error: (err) => {
        this.saving = false;
        this.modalError = this.errorText(err) || this.i18n.t('accounting.fiscal.openError');
      }
    });
  }

  toggleLock(period: FiscalPeriod): void {
    if (!this.canManage || this.togglingPeriodId !== null) return;
    const label = this.periodLabel(period);
    const confirmKey = period.isLocked ? 'accounting.fiscal.confirmUnlock' : 'accounting.fiscal.confirmLock';
    if (!confirm(this.i18n.t(confirmKey, { period: label }))) return;

    this.togglingPeriodId = period.id;
    this.actionError = '';
    const req = period.isLocked
      ? this.accountingChart.unlockPeriod(period.id)
      : this.accountingChart.lockPeriod(period.id);
    req.subscribe({
      next: (updated) => {
        this.togglingPeriodId = null;
        if (this.selected) {
          const idx = this.selected.periods.findIndex(p => p.id === period.id);
          if (idx >= 0) this.selected.periods[idx] = updated;
        }
      },
      error: (err) => {
        this.togglingPeriodId = null;
        this.actionError = this.errorText(err) || this.i18n.t('accounting.fiscal.lockError');
      }
    });
  }

  private errorText(err: unknown): string {
    const e = err as { error?: unknown };
    if (typeof e?.error === 'string') return e.error;
    const obj = e?.error as { error?: string; message?: string } | undefined;
    return obj?.error || obj?.message || '';
  }
}
