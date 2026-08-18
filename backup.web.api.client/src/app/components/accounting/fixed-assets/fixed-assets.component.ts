import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MaterialModule } from '../../../material.module';
import { FixedAsset, FixedAssetForm } from '../../../models/accounting';
import { FixedAssetsApiService } from '../../../services/fixed-assets.service';
import { PermissionService } from '../../../services/permission.service';
import { Permissions } from '../../../constants/permissions';
import { AppI18nService } from '../../../services/app-i18n.service';
import { TPipe } from '../../../pipes/t.pipe';
import { FormHelpComponent } from '../../shared/form-help/form-help.component';

@Component({
  selector: 'app-fixed-assets',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, MaterialModule, TPipe, FormHelpComponent],
  templateUrl: './fixed-assets.component.html',
  styleUrls: ['./fixed-assets.component.css']
})
export class FixedAssetsComponent implements OnInit {
  readonly months = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
  readonly modes = ['Lineaire', 'Degressif'];

  assets: FixedAsset[] = [];
  selected: FixedAsset | null = null;
  loading = false;
  acting = false;
  saving = false;
  actionMessage = '';
  actionError = '';
  modalError = '';
  showModal = false;
  editingId: number | null = null;
  year = new Date().getFullYear();
  month = new Date().getMonth() + 1;
  form: FormGroup;

  constructor(
    private api: FixedAssetsApiService,
    public perm: PermissionService,
    private i18n: AppI18nService,
    private fb: FormBuilder
  ) {
    this.form = this.fb.group({
      code: [''],
      designation: ['', Validators.required],
      assetAccountCode: [''],
      depreciationAccountCode: [''],
      expenseAccountCode: [''],
      acquisitionDate: ['', Validators.required],
      serviceDate: ['', Validators.required],
      originValue: [0, [Validators.required, Validators.min(0.01)]],
      residualValue: [0, [Validators.min(0)]],
      durationMonths: [36, [Validators.required, Validators.min(1)]],
      mode: ['Lineaire', Validators.required]
    });
  }

  get canCreate(): boolean {
    return this.perm.has(Permissions.AccountingCreate);
  }

  get canValidate(): boolean {
    return this.perm.has(Permissions.AccountingValidate);
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.actionError = '';
    this.api.list().subscribe({
      next: (assets) => {
        this.assets = assets || [];
        this.loading = false;
        if (this.selected?.id) this.select(this.selected.id);
      },
      error: (err) => {
        this.assets = [];
        this.loading = false;
        this.actionError = this.errorText(err) || this.i18n.t('immo.loadError');
      }
    });
  }

  select(id: number): void {
    this.api.get(id).subscribe({
      next: (asset) => {
        this.selected = asset;
      },
      error: (err) => {
        this.actionError = this.errorText(err) || this.i18n.t('immo.loadError');
      }
    });
  }

  openCreate(): void {
    this.editingId = null;
    this.modalError = '';
    const today = new Date().toISOString().slice(0, 10);
    this.form.reset({
      code: '',
      designation: '',
      assetAccountCode: '',
      depreciationAccountCode: '',
      expenseAccountCode: '',
      acquisitionDate: today,
      serviceDate: today,
      originValue: 0,
      residualValue: 0,
      durationMonths: 36,
      mode: 'Lineaire'
    });
    this.form.enable();
    this.showModal = true;
  }

  openEdit(asset: FixedAsset, event?: Event): void {
    event?.stopPropagation();
    this.editingId = asset.id;
    this.modalError = '';
    this.form.reset({
      code: asset.code,
      designation: asset.designation,
      assetAccountCode: asset.assetAccountCode,
      depreciationAccountCode: asset.depreciationAccountCode,
      expenseAccountCode: asset.expenseAccountCode,
      acquisitionDate: (asset.acquisitionDate || '').slice(0, 10),
      serviceDate: (asset.serviceDate || '').slice(0, 10),
      originValue: asset.originValue,
      residualValue: asset.residualValue,
      durationMonths: asset.durationMonths,
      mode: asset.mode || 'Lineaire'
    });
    this.form.enable();
    if (asset.schedule?.some(s => s.isPosted)) {
      this.form.get('code')?.disable();
      this.form.get('assetAccountCode')?.disable();
      this.form.get('depreciationAccountCode')?.disable();
      this.form.get('expenseAccountCode')?.disable();
      this.form.get('acquisitionDate')?.disable();
      this.form.get('serviceDate')?.disable();
      this.form.get('originValue')?.disable();
      this.form.get('residualValue')?.disable();
      this.form.get('durationMonths')?.disable();
      this.form.get('mode')?.disable();
    }
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
    const raw = this.form.getRawValue();
    const body: FixedAssetForm = {
      code: (raw.code || '').trim() || null,
      designation: (raw.designation || '').trim(),
      assetAccountCode: (raw.assetAccountCode || '').trim() || null,
      depreciationAccountCode: (raw.depreciationAccountCode || '').trim() || null,
      expenseAccountCode: (raw.expenseAccountCode || '').trim() || null,
      acquisitionDate: raw.acquisitionDate,
      serviceDate: raw.serviceDate,
      originValue: +raw.originValue || 0,
      residualValue: +raw.residualValue || 0,
      durationMonths: +raw.durationMonths || 1,
      mode: raw.mode
    };
    this.saving = true;
    this.modalError = '';
    const request = this.editingId
      ? this.api.update(this.editingId, body)
      : this.api.create(body);
    request.subscribe({
      next: (saved) => {
        this.saving = false;
        this.showModal = false;
        this.actionMessage = this.editingId
          ? this.i18n.t('immo.updated', { code: saved.code })
          : this.i18n.t('immo.created', { code: saved.code });
        this.selected = saved;
        this.load();
      },
      error: (err) => {
        this.saving = false;
        this.modalError = this.errorText(err) || this.i18n.t('immo.saveError');
      }
    });
  }

  recalculate(asset: FixedAsset, event?: Event): void {
    event?.stopPropagation();
    if (!this.canCreate) return;
    this.acting = true;
    this.actionMessage = '';
    this.actionError = '';
    this.api.recalculate(asset.id).subscribe({
      next: (saved) => {
        this.acting = false;
        this.selected = saved;
        this.actionMessage = this.i18n.t('immo.recalculated', { code: saved.code });
        this.load();
      },
      error: (err) => {
        this.acting = false;
        this.actionError = this.errorText(err) || this.i18n.t('immo.actionError');
      }
    });
  }

  deactivate(asset: FixedAsset, event?: Event): void {
    event?.stopPropagation();
    if (!this.canValidate) return;
    if (!confirm(this.i18n.t('immo.confirmDeactivate', { code: asset.code }))) return;
    this.acting = true;
    this.actionMessage = '';
    this.actionError = '';
    this.api.deactivate(asset.id).subscribe({
      next: (saved) => {
        this.acting = false;
        this.selected = saved;
        this.actionMessage = this.i18n.t('immo.deactivated', { code: saved.code });
        this.load();
      },
      error: (err) => {
        this.acting = false;
        this.actionError = this.errorText(err) || this.i18n.t('immo.actionError');
      }
    });
  }

  postMonth(): void {
    if (!this.canValidate) return;
    if (!confirm(this.i18n.t('immo.confirmPost', { month: this.month, year: this.year }))) return;
    this.acting = true;
    this.actionMessage = '';
    this.actionError = '';
    this.api.postMonth(this.year, this.month).subscribe({
      next: (result) => {
        this.acting = false;
        this.actionMessage = this.i18n.t('immo.posted', {
          count: result.postedLines,
          number: result.entryNumber || '—'
        });
        this.load();
      },
      error: (err) => {
        this.acting = false;
        this.actionError = this.errorText(err) || this.i18n.t('immo.actionError');
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
