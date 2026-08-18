import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MaterialModule } from '../../../material.module';
import { PayrollEmployee, PayrollEmployeeForm, PayrollPeriodSummary } from '../../../models/accounting';
import { PayrollApiService } from '../../../services/payroll.service';
import { PermissionService } from '../../../services/permission.service';
import { Permissions } from '../../../constants/permissions';
import { AppI18nService } from '../../../services/app-i18n.service';
import { TPipe } from '../../../pipes/t.pipe';
import { downloadBlob, fileNameFromContentDisposition } from '../../../utils/download-blob.util';
import { FormHelpComponent } from '../../shared/form-help/form-help.component';
import { FieldHelpComponent } from '../../shared/field-help/field-help.component';

@Component({
  selector: 'app-payroll',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, MaterialModule, TPipe, FormHelpComponent, FieldHelpComponent],
  templateUrl: './payroll.component.html',
  styleUrls: ['./payroll.component.css']
})
export class PayrollComponent implements OnInit {
  readonly months = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];

  employees: PayrollEmployee[] = [];
  period: PayrollPeriodSummary | null = null;
  year = new Date().getFullYear();
  month = new Date().getMonth() + 1;
  loading = false;
  acting = false;
  saving = false;
  downloading = false;
  actionMessage = '';
  actionError = '';
  modalError = '';
  showModal = false;
  editingId: number | null = null;
  form: FormGroup;

  constructor(
    private api: PayrollApiService,
    public perm: PermissionService,
    private i18n: AppI18nService,
    private fb: FormBuilder
  ) {
    this.form = this.fb.group({
      lastName: ['', Validators.required],
      firstName: ['', Validators.required],
      cnssNumber: [''],
      baseSalary: [0, [Validators.required, Validators.min(0)]],
      overtime: [0],
      bonuses: [0],
      benefitsInKind: [0],
      hireDate: ['', Validators.required],
      exitDate: [''],
      isActive: [true]
    });
  }

  get canCreate(): boolean {
    return this.perm.has(Permissions.AccountingCreate);
  }

  get canValidate(): boolean {
    return this.perm.has(Permissions.AccountingValidate);
  }

  ngOnInit(): void {
    this.loadEmployees();
    this.loadPayslips();
  }

  loadEmployees(): void {
    this.api.listEmployees().subscribe({
      next: (rows) => { this.employees = rows || []; },
      error: (err) => {
        this.employees = [];
        this.actionError = this.errorText(err) || this.i18n.t('payroll.loadError');
      }
    });
  }

  loadPayslips(): void {
    this.loading = true;
    this.actionError = '';
    this.api.listPayslips(this.year, this.month).subscribe({
      next: (dto) => {
        this.period = dto;
        this.loading = false;
      },
      error: (err) => {
        this.period = null;
        this.loading = false;
        this.actionError = this.errorText(err) || this.i18n.t('payroll.loadError');
      }
    });
  }

  openCreate(): void {
    this.editingId = null;
    this.modalError = '';
    this.form.reset({
      lastName: '',
      firstName: '',
      cnssNumber: '',
      baseSalary: 0,
      overtime: 0,
      bonuses: 0,
      benefitsInKind: 0,
      hireDate: new Date().toISOString().slice(0, 10),
      exitDate: '',
      isActive: true
    });
    this.showModal = true;
  }

  openEdit(employee: PayrollEmployee): void {
    this.editingId = employee.id;
    this.modalError = '';
    this.form.reset({
      lastName: employee.lastName,
      firstName: employee.firstName,
      cnssNumber: employee.cnssNumber || '',
      baseSalary: employee.baseSalary,
      overtime: employee.overtime,
      bonuses: employee.bonuses,
      benefitsInKind: employee.benefitsInKind,
      hireDate: (employee.hireDate || '').slice(0, 10),
      exitDate: employee.exitDate ? employee.exitDate.slice(0, 10) : '',
      isActive: employee.isActive
    });
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
    const body: PayrollEmployeeForm = {
      lastName: (v.lastName || '').trim(),
      firstName: (v.firstName || '').trim(),
      cnssNumber: (v.cnssNumber || '').trim() || null,
      baseSalary: +v.baseSalary || 0,
      overtime: +v.overtime || 0,
      bonuses: +v.bonuses || 0,
      benefitsInKind: +v.benefitsInKind || 0,
      hireDate: v.hireDate,
      exitDate: v.exitDate || null,
      isActive: !!v.isActive
    };
    this.saving = true;
    this.modalError = '';
    const request = this.editingId
      ? this.api.updateEmployee(this.editingId, body)
      : this.api.createEmployee(body);
    request.subscribe({
      next: (saved) => {
        this.saving = false;
        this.showModal = false;
        this.actionMessage = this.i18n.t('payroll.employeeSaved', {
          name: `${saved.lastName} ${saved.firstName}`.trim()
        });
        this.loadEmployees();
      },
      error: (err) => {
        this.saving = false;
        this.modalError = this.errorText(err) || this.i18n.t('payroll.saveError');
      }
    });
  }

  calculateAll(): void {
    if (!this.canCreate) return;
    this.acting = true;
    this.actionMessage = '';
    this.actionError = '';
    this.api.calculate(this.year, this.month).subscribe({
      next: (result) => {
        this.acting = false;
        const count = (result as { count?: number })?.count ?? 0;
        this.actionMessage = this.i18n.t('payroll.calculated', { count });
        this.loadPayslips();
      },
      error: (err) => {
        this.acting = false;
        this.actionError = this.errorText(err) || this.i18n.t('payroll.actionError');
      }
    });
  }

  postMonth(): void {
    if (!this.canValidate) return;
    if (!confirm(this.i18n.t('payroll.confirmPost', { month: this.month, year: this.year }))) return;
    this.acting = true;
    this.actionMessage = '';
    this.actionError = '';
    this.api.postMonth(this.year, this.month).subscribe({
      next: (result) => {
        this.acting = false;
        this.actionMessage = this.i18n.t('payroll.posted', {
          count: result.postedCount,
          number: result.entryNumber || '—'
        });
        this.loadPayslips();
      },
      error: (err) => {
        this.acting = false;
        this.actionError = this.errorText(err) || this.i18n.t('payroll.actionError');
      }
    });
  }

  downloadCnss(format?: string): void {
    if (this.downloading) return;
    this.downloading = true;
    this.actionMessage = '';
    this.actionError = '';
    const ext = format === 'xml' ? 'xml' : 'txt';
    this.api.downloadCnss(this.year, this.month, format).subscribe({
      next: (response) => {
        const blob = response.body;
        if (blob) {
          const name = fileNameFromContentDisposition(
            response.headers.get('Content-Disposition'),
            `CNSS_${this.year}${String(this.month).padStart(2, '0')}.${ext}`);
          downloadBlob(blob, name);
          this.actionMessage = this.i18n.t('exports.downloaded', { fileName: name });
        }
        this.downloading = false;
        this.loadPayslips();
      },
      error: (err) => {
        this.downloading = false;
        void this.readBlobError(err).then(text => {
          this.actionError = text || this.i18n.t('payroll.actionError');
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
