import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MaterialModule } from '../../../material.module';
import {
  CabinetAnnotation, CabinetApiService, CabinetCompanyOption, CabinetDossier, CabinetEntry
} from '../../../services/cabinet.service';
import { PermissionService } from '../../../services/permission.service';
import { Permissions } from '../../../constants/permissions';
import { AppI18nService } from '../../../services/app-i18n.service';
import { TPipe } from '../../../pipes/t.pipe';
import { FormHelpComponent } from '../../shared/form-help/form-help.component';

@Component({
  selector: 'app-cabinet-portal',
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, TPipe, FormHelpComponent],
  templateUrl: './cabinet-portal.component.html',
  styleUrls: ['./cabinet-portal.component.css']
})
export class CabinetPortalComponent implements OnInit {
  dossiers: CabinetDossier[] = [];
  companies: CabinetCompanyOption[] = [];
  selected: CabinetDossier | null = null;
  entries: CabinetEntry[] = [];
  annotations: CabinetAnnotation[] = [];
  linkCompanyId = '';
  linkLevel = 'Revue';
  noteType = 'Question';
  noteMessage = '';
  closeYear = new Date().getFullYear();
  closeMonth = new Date().getMonth() + 1;
  loading = false;
  acting = false;
  actionMessage = '';
  actionError = '';

  constructor(
    private api: CabinetApiService,
    public perm: PermissionService,
    private i18n: AppI18nService
  ) {}

  get canManage(): boolean {
    return this.perm.has(Permissions.AccountingValidate) || this.perm.has(Permissions.AccountingCabinet);
  }

  ngOnInit(): void {
    this.load();
    this.api.companies().subscribe({ next: rows => this.companies = rows || [] });
  }

  load(): void {
    this.loading = true;
    this.api.dossiers().subscribe({
      next: rows => {
        this.dossiers = rows || [];
        this.loading = false;
        if (this.selected) {
          const again = this.dossiers.find(d => d.companyId === this.selected!.companyId);
          if (again) this.open(again);
        }
      },
      error: err => { this.loading = false; this.actionError = this.errorText(err); }
    });
  }

  open(dossier: CabinetDossier): void {
    this.selected = dossier;
    this.api.entries(dossier.companyId).subscribe({ next: rows => this.entries = rows || [] });
    this.api.annotations(dossier.companyId).subscribe({ next: rows => this.annotations = rows || [] });
  }

  link(): void {
    if (!this.canManage || !this.linkCompanyId) return;
    this.acting = true;
    this.api.link(this.linkCompanyId, this.linkLevel).subscribe({
      next: dto => {
        this.acting = false;
        this.actionMessage = this.i18n.t('cabinet.linked', { name: dto.name });
        this.load();
        this.open(dto);
      },
      error: err => { this.acting = false; this.actionError = this.errorText(err); }
    });
  }

  addNote(): void {
    if (!this.canManage || !this.selected || !this.noteMessage.trim()) return;
    this.acting = true;
    this.api.annotate(this.selected.companyId, this.noteMessage.trim(), this.noteType).subscribe({
      next: () => {
        this.acting = false;
        this.noteMessage = '';
        this.open(this.selected!);
      },
      error: err => { this.acting = false; this.actionError = this.errorText(err); }
    });
  }

  resolve(note: CabinetAnnotation): void {
    if (!this.canManage) return;
    this.api.resolve(note.id).subscribe({
      next: () => this.selected && this.open(this.selected),
      error: err => this.actionError = this.errorText(err)
    });
  }

  closePeriod(force: boolean): void {
    if (!this.canManage || !this.selected) return;
    if (!confirm(this.i18n.t('cabinet.confirmClose', { month: this.closeMonth, year: this.closeYear }))) return;
    this.acting = true;
    this.api.validateClose(this.selected.companyId, this.closeYear, this.closeMonth, force).subscribe({
      next: res => {
        this.acting = false;
        this.actionMessage = res.message;
        this.load();
      },
      error: err => { this.acting = false; this.actionError = this.errorText(err); }
    });
  }

  private errorText(err: unknown): string {
    const e = err as { error?: unknown };
    if (typeof e?.error === 'string') return e.error;
    const obj = e?.error as { error?: string; message?: string } | undefined;
    return obj?.error || obj?.message || '';
  }
}
