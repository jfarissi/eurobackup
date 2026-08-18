import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MaterialModule } from '../../../material.module';
import { BankReconciliationApiService } from '../../../services/bank-reconciliation.service';
import { BankLedgerCandidate, BankReconciliation, BankStatementLine } from '../../../models/accounting';
import { PermissionService } from '../../../services/permission.service';
import { Permissions } from '../../../constants/permissions';
import { AppI18nService } from '../../../services/app-i18n.service';
import { TPipe } from '../../../pipes/t.pipe';
import { FormHelpComponent } from '../../shared/form-help/form-help.component';
import { FieldHelpComponent } from '../../shared/field-help/field-help.component';

@Component({
  selector: 'app-bank-reconciliation',
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, TPipe, FormHelpComponent, FieldHelpComponent],
  templateUrl: './bank-reconciliation.component.html',
  styleUrls: ['./bank-reconciliation.component.css']
})
export class BankReconciliationComponent implements OnInit {
  readonly P = Permissions;

  items: BankReconciliation[] = [];
  current: BankReconciliation | null = null;
  accountCode = '';
  selectedStatementLineId: number | null = null;

  loading = false;
  acting = false;
  actionMessage = '';
  actionError = '';

  constructor(
    private api: BankReconciliationApiService,
    public perm: PermissionService,
    private i18n: AppI18nService
  ) {}

  get canCreate(): boolean {
    return this.perm.has(Permissions.AccountingCreate);
  }

  get canValidate(): boolean {
    return this.perm.has(Permissions.AccountingValidate);
  }

  get isOpen(): boolean {
    return this.current?.status === 'Open';
  }

  get allMatched(): boolean {
    return !!this.current && this.current.lineCount > 0 && this.current.matchedCount === this.current.lineCount;
  }

  get selectedLine(): BankStatementLine | null {
    return this.current?.lines.find(l => l.id === this.selectedStatementLineId) ?? null;
  }

  ngOnInit(): void {
    this.loadList();
  }

  loadList(selectId?: number): void {
    this.loading = true;
    this.actionError = '';
    this.api.list().subscribe({
      next: (items) => {
        this.items = items || [];
        this.loading = false;
        const id = selectId ?? this.current?.id;
        if (id) this.open(id);
        else if (this.items.length) this.open(this.items[0].id);
      },
      error: (err) => {
        this.loading = false;
        this.actionError = this.errorText(err) || this.i18n.t('bankrec.loadError');
      }
    });
  }

  open(id: number): void {
    this.loading = true;
    this.actionError = '';
    this.selectedStatementLineId = null;
    this.api.get(id).subscribe({
      next: (dto) => {
        this.current = dto;
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        this.actionError = this.errorText(err) || this.i18n.t('bankrec.loadError');
      }
    });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file || !this.canCreate) return;
    this.importFile(file);
    input.value = '';
  }

  importFile(file: File): void {
    this.acting = true;
    this.actionMessage = '';
    this.actionError = '';
    this.api.import(file, this.accountCode).subscribe({
      next: (dto) => {
        this.current = dto;
        this.acting = false;
        this.actionMessage = this.i18n.t('bankrec.imported');
        this.loadList(dto.id);
      },
      error: (err) => {
        this.acting = false;
        this.actionError = this.errorText(err) || this.i18n.t('bankrec.actionError');
      }
    });
  }

  autoMatch(): void {
    if (!this.current || !this.canValidate || !this.isOpen) return;
    this.acting = true;
    this.actionMessage = '';
    this.actionError = '';
    this.api.autoMatch(this.current.id).subscribe({
      next: (result) => {
        this.current = result.reconciliation ?? this.current;
        this.acting = false;
        this.actionMessage = this.i18n.t('bankrec.matched', { count: result.matched });
        this.refreshListCounts();
      },
      error: (err) => {
        this.acting = false;
        this.actionError = this.errorText(err) || this.i18n.t('bankrec.actionError');
      }
    });
  }

  selectStatementLine(line: BankStatementLine): void {
    if (!this.isOpen || line.isMatched) return;
    this.selectedStatementLineId = this.selectedStatementLineId === line.id ? null : line.id;
    this.actionError = '';
  }

  canMatchLedger(candidate: BankLedgerCandidate): boolean {
    const line = this.selectedLine;
    return !!line && this.amountMatches(line, candidate);
  }

  matchLedger(candidate: BankLedgerCandidate): void {
    if (!this.current || !this.canValidate || !this.isOpen) return;
    const line = this.selectedLine;
    if (!line) {
      this.actionError = this.i18n.t('bankrec.pickLine');
      return;
    }
    if (!this.amountMatches(line, candidate)) {
      this.actionError = this.i18n.t('bankrec.amountMismatch');
      return;
    }
    this.acting = true;
    this.actionMessage = '';
    this.actionError = '';
    this.api.manualMatch(this.current.id, line.id, candidate.lineId).subscribe({
      next: (dto) => {
        this.current = dto;
        this.selectedStatementLineId = null;
        this.acting = false;
        this.actionMessage = this.i18n.t('bankrec.manualOk');
        this.refreshListCounts();
      },
      error: (err) => {
        this.acting = false;
        this.actionError = this.errorText(err) || this.i18n.t('bankrec.actionError');
      }
    });
  }

  unmatch(line: BankStatementLine, event: Event): void {
    event.stopPropagation();
    if (!this.current || !this.canValidate || !this.isOpen || !line.isMatched) return;
    this.acting = true;
    this.actionMessage = '';
    this.actionError = '';
    this.api.unmatch(this.current.id, line.id).subscribe({
      next: (dto) => {
        this.current = dto;
        this.acting = false;
        this.actionMessage = this.i18n.t('bankrec.unmatchedOk');
        this.refreshListCounts();
      },
      error: (err) => {
        this.acting = false;
        this.actionError = this.errorText(err) || this.i18n.t('bankrec.actionError');
      }
    });
  }

  complete(): void {
    if (!this.current || !this.canValidate || !this.isOpen || !this.allMatched) return;
    if (!confirm(this.i18n.t('bankrec.confirmComplete'))) return;
    this.acting = true;
    this.actionMessage = '';
    this.actionError = '';
    this.api.complete(this.current.id).subscribe({
      next: (dto) => {
        this.current = dto;
        this.acting = false;
        this.actionMessage = this.i18n.t('bankrec.completed');
        this.refreshListCounts();
      },
      error: (err) => {
        this.acting = false;
        this.actionError = this.errorText(err) || this.i18n.t('bankrec.actionError');
      }
    });
  }

  methodLabel(method?: string | null): string {
    if (!method) return '';
    const key = `bankrec.method.${method}`;
    const translated = this.i18n.t(key);
    return translated === key ? method : translated;
  }

  private amountMatches(line: BankStatementLine, candidate: BankLedgerCandidate): boolean {
    if (line.credit > 0) return Math.abs(line.credit - candidate.debit) < 0.01;
    if (line.debit > 0) return Math.abs(line.debit - candidate.credit) < 0.01;
    return false;
  }

  private refreshListCounts(): void {
    if (!this.current) return;
    this.items = this.items.map(item =>
      item.id === this.current!.id
        ? { ...item, status: this.current!.status, matchedCount: this.current!.matchedCount, lineCount: this.current!.lineCount }
        : item
    );
  }

  private errorText(err: unknown): string {
    const e = err as { error?: unknown };
    if (typeof e?.error === 'string') return e.error;
    const obj = e?.error as { error?: string; message?: string } | undefined;
    return obj?.error || obj?.message || '';
  }
}
