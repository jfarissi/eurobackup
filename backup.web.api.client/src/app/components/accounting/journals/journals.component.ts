import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MaterialModule } from '../../../material.module';
import { AccountingChartService } from '../../../services/accounting-chart.service';
import { Journal, JournalForm } from '../../../models/accounting';
import { PermissionService } from '../../../services/permission.service';
import { Permissions } from '../../../constants/permissions';
import { AppI18nService } from '../../../services/app-i18n.service';
import { TPipe } from '../../../pipes/t.pipe';
import { FormHelpComponent } from '../../shared/form-help/form-help.component';
import { FieldHelpComponent } from '../../shared/field-help/field-help.component';

@Component({
  selector: 'app-journals',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MaterialModule, TPipe, FormHelpComponent, FieldHelpComponent],
  templateUrl: './journals.component.html',
  styleUrls: ['./journals.component.css']
})
export class JournalsComponent implements OnInit {
  readonly P = Permissions;
  readonly journalCodes = ['ACH', 'VEN', 'BAN', 'CAIS', 'OD', 'AN', 'SAL'];

  journals: Journal[] = [];
  loading = false;
  saving = false;
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
      code: ['', Validators.required],
      label: ['', Validators.required],
      counterpartAccountCode: ['']
    });
  }

  get canManage(): boolean {
    return this.perm.has(Permissions.AccountingManagePlan);
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.actionError = '';
    this.accountingChart.getJournals().subscribe({
      next: (journals) => {
        this.journals = journals || [];
        this.loading = false;
      },
      error: (err) => {
        this.journals = [];
        this.loading = false;
        this.actionError = this.errorText(err) || this.i18n.t('accounting.journals.loadError');
      }
    });
  }

  openCreate(): void {
    this.modalError = '';
    this.form.reset({ code: '', label: '', counterpartAccountCode: '' });
    this.showModal = true;
  }

  closeModal(): void {
    if (this.saving) return;
    this.showModal = false;
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.value;
    const body: JournalForm = {
      code: (v.code || '').trim(),
      label: (v.label || '').trim(),
      counterpartAccountCode: (v.counterpartAccountCode || '').trim() || null
    };
    this.saving = true;
    this.modalError = '';
    this.accountingChart.createJournal(body).subscribe({
      next: (created) => {
        this.saving = false;
        this.showModal = false;
        this.actionMessage = this.i18n.t('accounting.journals.created', { code: created.code });
        this.load();
      },
      error: (err) => {
        this.saving = false;
        this.modalError = this.errorText(err) || this.i18n.t('accounting.journals.saveError');
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
