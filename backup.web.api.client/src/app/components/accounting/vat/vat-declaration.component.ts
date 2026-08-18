import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MaterialModule } from '../../../material.module';
import { VatDeclarationApiService } from '../../../services/vat-declaration.service';
import { VatDeclaration } from '../../../models/accounting';
import { PermissionService } from '../../../services/permission.service';
import { Permissions } from '../../../constants/permissions';
import { AppI18nService } from '../../../services/app-i18n.service';
import { TPipe } from '../../../pipes/t.pipe';
import { downloadBlob, fileNameFromContentDisposition } from '../../../utils/download-blob.util';
import { FormHelpComponent } from '../../shared/form-help/form-help.component';
import { FieldHelpComponent } from '../../shared/field-help/field-help.component';

@Component({
  selector: 'app-vat-declaration',
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, TPipe, FormHelpComponent, FieldHelpComponent],
  templateUrl: './vat-declaration.component.html',
  styleUrls: ['./vat-declaration.component.css']
})
export class VatDeclarationComponent implements OnInit {
  readonly P = Permissions;
  readonly months = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];

  year = new Date().getFullYear();
  month = new Date().getMonth() + 1;
  declaration: VatDeclaration | null = null;
  loading = false;
  acting = false;
  actionMessage = '';
  actionError = '';

  constructor(
    private api: VatDeclarationApiService,
    public perm: PermissionService,
    private i18n: AppI18nService
  ) {}

  get canValidate(): boolean {
    return this.perm.has(Permissions.AccountingValidate);
  }

  get isDeclared(): boolean {
    return this.declaration?.status === 'Declared';
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.actionError = '';
    this.api.get(this.year, this.month).subscribe({
      next: (dto) => {
        this.declaration = dto;
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        this.actionError = this.errorText(err) || this.i18n.t('vat.loadError');
      }
    });
  }

  declare(): void {
    if (!this.canValidate || this.isDeclared) return;
    if (!confirm(this.i18n.t('vat.confirmDeclare', { month: this.month, year: this.year }))) return;
    this.acting = true;
    this.actionMessage = '';
    this.actionError = '';
    this.api.declare(this.year, this.month).subscribe({
      next: (dto) => {
        this.declaration = dto;
        this.acting = false;
        this.actionMessage = this.i18n.t('vat.declared');
      },
      error: (err) => {
        this.acting = false;
        this.actionError = this.errorText(err) || this.i18n.t('vat.actionError');
      }
    });
  }

  undeclare(): void {
    if (!this.canValidate || !this.isDeclared) return;
    if (!confirm(this.i18n.t('vat.confirmUndeclare', { month: this.month, year: this.year }))) return;
    this.acting = true;
    this.actionMessage = '';
    this.actionError = '';
    this.api.undeclare(this.year, this.month).subscribe({
      next: () => {
        this.acting = false;
        this.actionMessage = this.i18n.t('vat.undeclared');
        this.load();
      },
      error: (err) => {
        this.acting = false;
        this.actionError = this.errorText(err) || this.i18n.t('vat.actionError');
      }
    });
  }

  downloadEdi(): void {
    if (this.acting || !this.declaration) return;
    this.acting = true;
    this.actionMessage = '';
    this.actionError = '';
    this.api.downloadEdi(this.year, this.month).subscribe({
      next: (response) => {
        const blob = response.body;
        if (blob) {
          const name = fileNameFromContentDisposition(
            response.headers.get('Content-Disposition'),
            `TVA_${String(this.month).padStart(2, '0')}_${this.year}.xml`);
          downloadBlob(blob, name);
          this.actionMessage = this.i18n.t('vat.ediDownloaded', { fileName: name });
        }
        this.acting = false;
      },
      error: (err) => {
        this.acting = false;
        void this.readBlobError(err).then(text => {
          this.actionError = text || this.i18n.t('vat.actionError');
        });
      }
    });
  }

  alertText(code: string): string {
    const key = `vat.alert.${code}`;
    const translated = this.i18n.t(key);
    return translated === key ? code : translated;
  }

  private errorText(err: unknown): string {
    const e = err as { error?: unknown };
    if (typeof e?.error === 'string') return e.error;
    const obj = e?.error as { error?: string; message?: string } | undefined;
    return obj?.error || obj?.message || '';
  }

  private async readBlobError(err: unknown): Promise<string> {
    const e = err as { error?: unknown };
    if (e?.error instanceof Blob) {
      try { return (await e.error.text()).trim(); } catch { return ''; }
    }
    return this.errorText(err);
  }
}
