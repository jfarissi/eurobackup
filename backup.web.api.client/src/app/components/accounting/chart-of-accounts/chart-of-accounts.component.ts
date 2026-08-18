import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MaterialModule } from '../../../material.module';
import { AccountingChartService } from '../../../services/accounting-chart.service';
import { ChartOfAccount, ChartOfAccountForm } from '../../../models/accounting';
import { PermissionService } from '../../../services/permission.service';
import { Permissions } from '../../../constants/permissions';
import { AppI18nService } from '../../../services/app-i18n.service';
import { TPipe } from '../../../pipes/t.pipe';
import { TableSortState } from '../../../utils/table-sort';
import { SortableThComponent } from '../../shared/sortable-th/sortable-th.component';
import { FormHelpComponent } from '../../shared/form-help/form-help.component';
import { FieldHelpComponent } from '../../shared/field-help/field-help.component';

@Component({
  selector: 'app-chart-of-accounts',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, MaterialModule, TPipe, SortableThComponent, FormHelpComponent, FieldHelpComponent],
  templateUrl: './chart-of-accounts.component.html',
  styleUrls: ['./chart-of-accounts.component.css']
})
export class ChartOfAccountsComponent implements OnInit {
  readonly P = Permissions;
  readonly accountClasses = [1, 2, 3, 4, 5, 6, 7, 8];
  readonly accountTypes = ['Actif', 'Passif', 'Charge', 'Produit', 'CapitauxPropres'];
  sort = new TableSortState('accountNumber', 'asc');

  accounts: ChartOfAccount[] = [];
  loading = false;
  saving = false;
  actionMessage = '';
  actionError = '';
  modalError = '';

  filterSearch = '';
  filterClass: number | '' = '';

  showModal = false;
  editing: ChartOfAccount | null = null;
  parentFilter = '';
  form: FormGroup;

  constructor(
    private accountingChart: AccountingChartService,
    public perm: PermissionService,
    private i18n: AppI18nService,
    private fb: FormBuilder
  ) {
    this.form = this.fb.group({
      accountNumber: ['', Validators.required],
      label: ['', Validators.required],
      labelArabic: [''],
      accountClass: [null as number | null, Validators.required],
      accountType: ['Actif', Validators.required],
      isLettrable: [false],
      isBilan: [false],
      isResultat: [false],
      parent: [null as ChartOfAccount | null]
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
    this.accountingChart.getAccounts(
      this.filterClass === '' ? undefined : this.filterClass,
      this.filterSearch || undefined
    ).subscribe({
      next: (accounts) => {
        this.accounts = accounts || [];
        this.loading = false;
      },
      error: (err) => {
        this.accounts = [];
        this.loading = false;
        this.actionError = this.errorText(err) || this.i18n.t('accounting.plan.loadError');
      }
    });
  }

  resetFilters(): void {
    this.filterSearch = '';
    this.filterClass = '';
    this.load();
  }

  get sortedAccounts(): ChartOfAccount[] {
    void this.sort.version;
    return this.sort.sort(this.accounts, {
      accountNumber: a => a.accountNumber ?? '',
      label: a => a.label ?? '',
      accountClass: a => a.accountClass,
      accountType: a => a.accountType ?? '',
      parent: a => this.parentLabel(a)
    });
  }

  parentLabel(a: ChartOfAccount): string {
    if (!a.parentId) return '';
    const parent = this.accounts.find(x => x.id === a.parentId);
    return parent ? `${parent.accountNumber} — ${parent.label}` : '';
  }

  parentDisplay(parent: ChartOfAccount | string | null): string {
    if (!parent) return '';
    if (typeof parent === 'string') return parent;
    return `${parent.accountNumber} — ${parent.label}`;
  }

  get parentOptions(): ChartOfAccount[] {
    const term = this.parentFilter.trim().toLowerCase();
    return this.accounts
      .filter(a => a.id !== this.editing?.id)
      .filter(a => !term
        || a.accountNumber.toLowerCase().includes(term)
        || a.label.toLowerCase().includes(term))
      .slice(0, 20);
  }

  openCreate(): void {
    this.editing = null;
    this.parentFilter = '';
    this.modalError = '';
    this.form.reset({
      accountNumber: '',
      label: '',
      labelArabic: '',
      accountClass: null,
      accountType: 'Actif',
      isLettrable: false,
      isBilan: false,
      isResultat: false,
      parent: null
    });
    this.showModal = true;
  }

  openEdit(account: ChartOfAccount): void {
    this.editing = account;
    this.parentFilter = '';
    this.modalError = '';
    this.form.reset({
      accountNumber: account.accountNumber,
      label: account.label,
      labelArabic: account.labelArabic ?? '',
      accountClass: account.accountClass,
      accountType: account.accountType || 'Actif',
      isLettrable: account.isLettrable,
      isBilan: account.isBilan,
      isResultat: account.isResultat,
      parent: this.accounts.find(x => x.id === account.parentId) ?? null
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
    const body: ChartOfAccountForm = {
      accountNumber: (v.accountNumber || '').trim(),
      label: (v.label || '').trim(),
      labelArabic: (v.labelArabic || '').trim() || null,
      accountClass: +v.accountClass,
      accountType: v.accountType,
      isLettrable: !!v.isLettrable,
      isBilan: !!v.isBilan,
      isResultat: !!v.isResultat,
      parentId: v.parent?.id ?? null
    };
    this.saving = true;
    this.modalError = '';
    const req = this.editing
      ? this.accountingChart.updateAccount(this.editing.id, body)
      : this.accountingChart.createAccount(body);
    req.subscribe({
      next: () => {
        this.saving = false;
        this.showModal = false;
        this.actionMessage = this.i18n.t('accounting.plan.saved');
        this.load();
      },
      error: (err) => {
        this.saving = false;
        this.modalError = this.errorText(err) || this.i18n.t('accounting.plan.saveError');
      }
    });
  }

  remove(account: ChartOfAccount): void {
    if (!this.canManage) return;
    if (!confirm(this.i18n.t('accounting.plan.confirmDelete', { number: account.accountNumber }))) return;
    this.actionError = '';
    this.actionMessage = '';
    this.accountingChart.deleteAccount(account.id).subscribe({
      next: () => {
        this.actionMessage = this.i18n.t('accounting.plan.deleted', { number: account.accountNumber });
        this.load();
      },
      error: (err) => {
        this.actionError = this.errorText(err) || this.i18n.t('accounting.plan.deleteError');
      }
    });
  }

  /** Le contrôleur renvoie BadRequest("message") : err.error est alors une chaîne. */
  private errorText(err: unknown): string {
    const e = err as { error?: unknown };
    if (typeof e?.error === 'string') return e.error;
    const obj = e?.error as { error?: string; message?: string } | undefined;
    return obj?.error || obj?.message || '';
  }
}
