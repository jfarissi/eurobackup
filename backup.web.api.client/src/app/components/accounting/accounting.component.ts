import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { MaterialModule } from '../../material.module';
import { BusinessService } from '../../services/business.service';
import { AccountingEntry, AccountingEntryLine, ManualAccountingEntryRequest, UnifiedPayment } from '../../models/business';
import { PermissionService } from '../../services/permission.service';
import { Permissions } from '../../constants/permissions';
import { AppI18nService } from '../../services/app-i18n.service';
import { TPipe } from '../../pipes/t.pipe';
import { FormHelpComponent } from '../shared/form-help/form-help.component';
import { FieldHelpComponent } from '../shared/field-help/field-help.component';
import { TableSortState } from '../../utils/table-sort';
import { SortableThComponent } from '../shared/sortable-th/sortable-th.component';

@Component({
  selector: 'app-accounting',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, MaterialModule, TPipe, FormHelpComponent, FieldHelpComponent, SortableThComponent],
  templateUrl: './accounting.component.html',
  styleUrls: ['./accounting.component.css']
})
export class AccountingComponent implements OnInit {
  readonly P = Permissions;
  selectedTab = 0;
  entrySort = new TableSortState('entryDate', 'desc');
  paymentSort = new TableSortState('date', 'desc');
  readonly journalTypes = [
    'SalesInvoice', 'CreditNote', 'SupplierInvoice', 'SupplierCreditNote',
    'Payment', 'SupplierPayment', 'Manual'
  ];
  readonly referenceTypes = [
    'SalesInvoice', 'CreditNote', 'SupplierInvoice', 'SupplierCreditNote',
    'SalesPayment', 'SupplierPayment', 'Manual'
  ];
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
  paymentsLoading = false;
  saving = false;
  actionMessage = '';
  actionError = '';

  filterSearch = '';
  filterJournalType = '';
  filterReferenceType = '';
  filterReferenceId: number | null = null;

  entries: AccountingEntry[] = [];
  selected: AccountingEntry | null = null;

  payments: UnifiedPayment[] = [];
  paymentSide: 'all' | 'sales' | 'purchases' = 'all';
  paymentStatus = '';
  paymentSearch = '';
  paymentFrom = '';
  paymentTo = '';

  showManualModal = false;
  editingEntry: AccountingEntry | null = null;
  manualDate = '';
  manualJournalType = 'Manual';
  manualDescription = '';
  manualReferenceType = 'Manual';
  manualReferenceId = 0;
  manualSaveAsDraft = false;
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

  onTabChange(index: number): void {
    this.selectedTab = index;
    this.actionError = '';
    if (index === 1) this.loadPayments();
    else this.loadEntries();
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

  loadPayments(): void {
    if (!this.perm.has(Permissions.InvoiceRead) && !this.perm.has(Permissions.SupplierInvoiceRead)) {
      this.payments = [];
      return;
    }
    this.paymentsLoading = true;
    this.actionError = '';
    this.businessService.getUnifiedPayments({
      side: this.paymentSide,
      status: this.paymentStatus || undefined,
      from: this.paymentFrom || undefined,
      to: this.paymentTo || undefined,
      search: this.paymentSearch || undefined
    }).subscribe({
      next: (rows) => {
        this.payments = rows || [];
        this.paymentsLoading = false;
      },
      error: (err) => {
        this.payments = [];
        this.paymentsLoading = false;
        this.actionError = err?.error?.error || err?.error || this.i18n.t('accounting.payments.loadError');
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

  resetPaymentFilters(): void {
    this.paymentSide = 'all';
    this.paymentStatus = '';
    this.paymentSearch = '';
    this.paymentFrom = '';
    this.paymentTo = '';
    this.loadPayments();
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
    this.editingEntry = null;
    this.manualDate = new Date().toISOString().slice(0, 10);
    this.manualJournalType = 'Manual';
    this.manualDescription = '';
    this.manualReferenceType = 'Manual';
    this.manualReferenceId = 0;
    this.manualSaveAsDraft = false;
    this.manualLines = [
      this.emptyLine(1),
      this.emptyLine(2)
    ];
    this.actionError = '';
    this.showManualModal = true;
  }

  /** Phase 3 : réutilise la modale de saisie pour modifier un brouillon (PUT). */
  openEditModal(entry: AccountingEntry): void {
    if (entry.status !== 'Draft' || !this.perm.has(Permissions.AccountingCreate)) return;
    const open = (full: AccountingEntry) => {
      const e = this.normalize(full);
      this.editingEntry = e;
      this.manualDate = (e.entryDate || '').slice(0, 10) || new Date().toISOString().slice(0, 10);
      this.manualJournalType = e.journalType || 'Manual';
      this.manualDescription = e.description || '';
      this.manualReferenceType = e.referenceType || 'Manual';
      this.manualReferenceId = e.referenceId || 0;
      this.manualSaveAsDraft = false;
      this.manualLines = (e.lines || []).map(l => ({ ...l }));
      if (this.manualLines.length < 2) {
        this.manualLines = [this.emptyLine(1), this.emptyLine(2)];
      }
      this.actionError = '';
      this.actionMessage = '';
      this.showManualModal = true;
    };
    if (entry.lines && entry.lines.length > 0) {
      open(entry);
    } else if (entry.id) {
      this.businessService.getAccountingEntry(entry.id).subscribe({ next: open });
    }
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
    const body: ManualAccountingEntryRequest = {
      entryDate: this.manualDate || undefined,
      journalType: this.manualJournalType,
      description: this.manualDescription,
      referenceType: this.manualReferenceType,
      referenceId: this.manualReferenceId || 0,
      saveAsDraft: !this.editingEntry && this.manualSaveAsDraft,
      lines
    };
    const editing = this.editingEntry;
    const request = editing?.id
      ? this.businessService.updateAccountingEntry(editing.id, body)
      : this.businessService.createAccountingEntry(body);
    request.subscribe({
      next: (saved) => {
        this.saving = false;
        this.showManualModal = false;
        this.actionMessage = editing
          ? this.i18n.t('accounting.entryUpdated', { number: saved.entryNumber })
          : this.manualSaveAsDraft
            ? this.i18n.t('accounting.draftSaved')
            : this.i18n.t('accounting.entryCreated', { number: saved.entryNumber });
        this.editingEntry = null;
        this.loadEntries();
        this.selectEntry(this.normalize(saved));
      },
      error: (err) => {
        this.saving = false;
        this.actionError = this.errorText(err) || this.i18n.t('accounting.saveError');
      }
    });
  }

  /** Phase 3 : suppression d'un brouillon (confirmation préalable). */
  deleteEntry(entry: AccountingEntry): void {
    if (entry.status !== 'Draft' || !this.perm.has(Permissions.AccountingCreate) || !entry.id) return;
    if (!confirm(this.i18n.t('accounting.confirmDeleteDraft', { number: entry.entryNumber }))) return;
    this.actionError = '';
    this.actionMessage = '';
    this.businessService.deleteAccountingEntry(entry.id).subscribe({
      next: () => {
        if (this.selected?.id === entry.id) this.selected = null;
        this.actionMessage = this.i18n.t('accounting.draftDeleted', { number: entry.entryNumber });
        this.loadEntries();
      },
      error: (err) => {
        this.actionError = this.errorText(err) || this.i18n.t('accounting.actionError');
      }
    });
  }

  /** Phase 3 : comptabilise un brouillon (Draft → Posted, permission Accounting.Validate côté API). */
  postEntry(entry: AccountingEntry): void {
    if (entry.status !== 'Draft' || !this.perm.has(Permissions.AccountingValidate) || !entry.id) return;
    this.actionError = '';
    this.actionMessage = '';
    this.businessService.postAccountingEntry(entry.id).subscribe({
      next: (posted) => {
        this.actionMessage = this.i18n.t('accounting.entryPosted', { number: posted.entryNumber });
        this.loadEntries();
      },
      error: (err) => {
        this.actionError = this.errorText(err) || this.i18n.t('accounting.actionError');
      }
    });
  }

  /** Phase 3 : valide une écriture comptabilisée (Posted → Validated, immuable). */
  validateEntry(entry: AccountingEntry): void {
    if (entry.status !== 'Posted' || !this.perm.has(Permissions.AccountingValidate) || !entry.id) return;
    if (!confirm(this.i18n.t('accounting.confirmValidate', { number: entry.entryNumber }))) return;
    this.actionError = '';
    this.actionMessage = '';
    this.businessService.validateAccountingEntry(entry.id).subscribe({
      next: (validated) => {
        this.actionMessage = this.i18n.t('accounting.entryValidated', { number: validated.entryNumber });
        this.loadEntries();
      },
      error: (err) => {
        this.actionError = this.errorText(err) || this.i18n.t('accounting.actionError');
      }
    });
  }

  /** Phase 3 : extourne une écriture (crée l'écriture inverse, originale → Reversed). */
  reverseEntry(entry: AccountingEntry): void {
    if (entry.status !== 'Posted' || !this.perm.has(Permissions.AccountingValidate) || !entry.id) return;
    if (!confirm(this.i18n.t('accounting.confirmReverse', { number: entry.entryNumber }))) return;
    this.actionError = '';
    this.actionMessage = '';
    this.businessService.reverseAccountingEntry(entry.id).subscribe({
      next: (reversal) => {
        this.actionMessage = this.i18n.t('accounting.entryReversed', { number: reversal.entryNumber });
        this.loadEntries();
      },
      error: (err) => {
        this.actionError = this.errorText(err) || this.i18n.t('accounting.actionError');
      }
    });
  }

  /** Libellé i18n du statut d'écriture (Draft/Posted/Validated/Reversed). */
  statusLabel(status: string | undefined): string {
    if (!status) return '';
    const key = `accounting.status.${status}`;
    const label = this.i18n.t(key);
    return label === key ? status : label;
  }

  entryDebit(entry: AccountingEntry): number {
    return (entry.lines || []).reduce((s, l) => s + (+l.debit || 0), 0);
  }

  entryCredit(entry: AccountingEntry): number {
    return (entry.lines || []).reduce((s, l) => s + (+l.credit || 0), 0);
  }

  get sortedEntries(): AccountingEntry[] {
    void this.entrySort.version;
    return this.entrySort.sort(this.entries, {
      entryNumber: e => e.entryNumber ?? '',
      entryDate: e => e.entryDate ?? '',
      journalType: e => e.journalType ?? '',
      reference: e => `${e.referenceType ?? ''} ${e.referenceId ?? ''}`.trim(),
      description: e => e.description ?? '',
      debit: e => this.entryDebit(e),
      credit: e => this.entryCredit(e),
      status: e => e.status ?? ''
    });
  }

  get sortedPayments(): UnifiedPayment[] {
    void this.paymentSort.version;
    return this.paymentSort.sort(this.payments, {
      date: p => p.date ?? '',
      side: p => p.side ?? '',
      documentNumber: p => p.documentNumber ?? '',
      partyName: p => p.partyName ?? '',
      amount: p => +p.amount || 0,
      method: p => p.method ?? '',
      reference: p => p.reference ?? '',
      status: p => p.status ?? ''
    });
  }

  paymentSideLabel(side: string): string {
    if (side === 'sales') return this.i18n.t('accounting.payments.side.sales');
    if (side === 'purchases') return this.i18n.t('accounting.payments.side.purchases');
    return side;
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

  /** Le contrôleur renvoie BadRequest("message") : err.error est alors une chaîne. */
  private errorText(err: unknown): string {
    const e = err as { error?: unknown };
    if (typeof e?.error === 'string') return e.error;
    const obj = e?.error as { error?: string; message?: string } | undefined;
    return obj?.error || obj?.message || '';
  }
}
