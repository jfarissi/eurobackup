import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MaterialModule } from '../../material.module';
import { BusinessService } from '../../services/business.service';
import { AccountingEntry, AccountingEntryLine } from '../../models/business';
import { PermissionService } from '../../services/permission.service';
import { Permissions } from '../../constants/permissions';
import { AppI18nService } from '../../services/app-i18n.service';
import { TPipe } from '../../pipes/t.pipe';
import { FormHelpComponent } from '../shared/form-help/form-help.component';

@Component({
  selector: 'app-accounting',
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, TPipe, FormHelpComponent],
  templateUrl: './accounting.component.html',
  styleUrls: ['./accounting.component.css']
})
export class AccountingComponent implements OnInit {
  readonly P = Permissions;
  readonly journalTypes = ['SalesInvoice', 'CreditNote', 'SupplierInvoice', 'Payment', 'Manual'];
  readonly referenceTypes = ['SalesInvoice', 'CreditNote', 'SupplierInvoice', 'SalesPayment', 'Manual'];
  readonly accountPresets: { code: string; label: string }[] = [
    { code: '411000', label: 'Clients' },
    { code: '401000', label: 'Fournisseurs' },
    { code: '701000', label: 'Ventes' },
    { code: '607000', label: 'Achats' },
    { code: '445710', label: 'TVA collectée' },
    { code: '445660', label: 'TVA déductible' },
    { code: '512000', label: 'Banque' },
    { code: '530000', label: 'Caisse' }
  ];

  loading = false;
  saving = false;
  actionMessage = '';
  actionError = '';

  filterSearch = '';
  filterJournalType = '';
  filterReferenceType = '';
  filterReferenceId: number | null = null;

  entries: AccountingEntry[] = [];
  selected: AccountingEntry | null = null;

  showManualModal = false;
  manualDate = '';
  manualJournalType = 'Manual';
  manualDescription = '';
  manualReferenceType = 'Manual';
  manualReferenceId = 0;
  manualLines: AccountingEntryLine[] = [];

  constructor(
    private businessService: BusinessService,
    public perm: PermissionService,
    private i18n: AppI18nService
  ) {}

  ngOnInit(): void {
    this.manualDate = new Date().toISOString().slice(0, 10);
    this.loadEntries();
  }

  loadEntries(): void {
    this.loading = true;
    this.actionError = '';
    this.businessService.getAccountingEntries({
      search: this.filterSearch || undefined,
      journalType: this.filterJournalType || undefined,
      referenceType: this.filterReferenceType || undefined,
      referenceId: this.filterReferenceId || undefined
    }).subscribe({
      next: (entries) => {
        this.entries = (entries || []).map(e => this.normalize(e));
        if (this.selected?.id) {
          this.selected = this.entries.find(e => e.id === this.selected!.id) || null;
        }
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        this.entries = [];
        this.actionError = err?.error?.error || err?.error || this.i18n.t('accounting.loadError');
      }
    });
  }

  resetFilters(): void {
    this.filterSearch = '';
    this.filterJournalType = '';
    this.filterReferenceType = '';
    this.filterReferenceId = null;
    this.loadEntries();
  }

  selectEntry(entry: AccountingEntry): void {
    this.selected = entry;
    if (entry.id && (!entry.lines || entry.lines.length === 0)) {
      this.businessService.getAccountingEntry(entry.id).subscribe({
        next: (full) => {
          this.selected = this.normalize(full);
          const idx = this.entries.findIndex(e => e.id === full.id);
          if (idx >= 0) this.entries[idx] = this.selected;
        }
      });
    }
  }

  openManualModal(): void {
    this.manualDate = new Date().toISOString().slice(0, 10);
    this.manualJournalType = 'Manual';
    this.manualDescription = '';
    this.manualReferenceType = 'Manual';
    this.manualReferenceId = 0;
    this.manualLines = [
      this.emptyLine(1),
      this.emptyLine(2)
    ];
    this.actionError = '';
    this.showManualModal = true;
  }

  addManualLine(): void {
    this.manualLines.push(this.emptyLine(this.manualLines.length + 1));
  }

  removeManualLine(index: number): void {
    if (this.manualLines.length <= 2) return;
    this.manualLines.splice(index, 1);
    this.manualLines.forEach((l, i) => l.lineNumber = i + 1);
  }

  applyAccountPreset(line: AccountingEntryLine, code: string): void {
    const preset = this.accountPresets.find(p => p.code === code);
    if (!preset) return;
    line.accountCode = preset.code;
    line.accountLabel = preset.label;
  }

  get manualDebitTotal(): number {
    return this.manualLines.reduce((s, l) => s + (+l.debit || 0), 0);
  }

  get manualCreditTotal(): number {
    return this.manualLines.reduce((s, l) => s + (+l.credit || 0), 0);
  }

  get isManualBalanced(): boolean {
    return Math.abs(this.manualDebitTotal - this.manualCreditTotal) < 0.015;
  }

  saveManualEntry(): void {
    if (!this.perm.has(Permissions.AccountingCreate)) return;
    if (!this.isManualBalanced) {
      this.actionError = this.i18n.t('accounting.unbalanced');
      return;
    }
    const lines = this.manualLines
      .filter(l => l.accountCode?.trim() && ((+l.debit || 0) > 0 || (+l.credit || 0) > 0))
      .map((l, i) => ({
        accountCode: l.accountCode.trim(),
        accountLabel: (l.accountLabel || l.accountCode).trim(),
        debit: +l.debit || 0,
        credit: +l.credit || 0,
        lineNumber: i + 1
      }));
    if (lines.length < 2) {
      this.actionError = this.i18n.t('accounting.minLines');
      return;
    }

    this.saving = true;
    this.actionError = '';
    this.businessService.createAccountingEntry({
      entryDate: this.manualDate || undefined,
      journalType: this.manualJournalType,
      description: this.manualDescription,
      referenceType: this.manualReferenceType,
      referenceId: this.manualReferenceId || 0,
      lines
    }).subscribe({
      next: (created) => {
        this.saving = false;
        this.showManualModal = false;
        this.actionMessage = this.i18n.t('accounting.entryCreated', { number: created.entryNumber });
        this.loadEntries();
        this.selectEntry(this.normalize(created));
      },
      error: (err) => {
        this.saving = false;
        this.actionError = typeof err?.error === 'string' ? err.error : (err?.error?.error || this.i18n.t('accounting.saveError'));
      }
    });
  }

  entryDebit(entry: AccountingEntry): number {
    return (entry.lines || []).reduce((s, l) => s + (+l.debit || 0), 0);
  }

  entryCredit(entry: AccountingEntry): number {
    return (entry.lines || []).reduce((s, l) => s + (+l.credit || 0), 0);
  }

  private emptyLine(n: number): AccountingEntryLine {
    return { accountCode: '', accountLabel: '', debit: 0, credit: 0, lineNumber: n };
  }

  private normalize(entry: AccountingEntry): AccountingEntry {
    return {
      ...entry,
      lines: (entry.lines || []).map((l, i) => ({
        ...l,
        debit: +l.debit || 0,
        credit: +l.credit || 0,
        lineNumber: l.lineNumber || i + 1
      }))
    };
  }
}
