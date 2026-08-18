import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MaterialModule } from '../../../material.module';
import { AccountingChartService } from '../../../services/accounting-chart.service';
import { LettrageService } from '../../../services/lettrage.service';
import { ChartOfAccount, LettrageAccountSummary, LettrageGroup, LettrageLine } from '../../../models/accounting';
import { PermissionService } from '../../../services/permission.service';
import { Permissions } from '../../../constants/permissions';
import { AppI18nService } from '../../../services/app-i18n.service';
import { TPipe } from '../../../pipes/t.pipe';
import { FormHelpComponent } from '../../shared/form-help/form-help.component';
import { FieldHelpComponent } from '../../shared/field-help/field-help.component';

/** Ligne non lettrée enrichie du solde cumulé (calculé côté client, dans l'ordre d'affichage). */
interface LettrageLineView extends LettrageLine {
  runningBalance: number;
}

@Component({
  selector: 'app-lettrage',
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, TPipe, FormHelpComponent, FieldHelpComponent],
  templateUrl: './lettrage.component.html',
  styleUrls: ['./lettrage.component.css']
})
export class LettrageComponent implements OnInit {
  readonly P = Permissions;

  accounts: ChartOfAccount[] = [];
  selectedAccount = '';

  unlettered: LettrageLineView[] = [];
  groups: LettrageGroup[] = [];
  autoSummaries: LettrageAccountSummary[] = [];
  selectedIds = new Set<number>();

  loading = false;
  groupsLoading = false;
  acting = false;
  actionMessage = '';
  actionError = '';

  constructor(
    private chart: AccountingChartService,
    private lettrage: LettrageService,
    public perm: PermissionService,
    private i18n: AppI18nService
  ) {}

  get canValidate(): boolean {
    return this.perm.has(Permissions.AccountingValidate);
  }

  ngOnInit(): void {
    this.chart.getAccounts().subscribe({
      next: (accounts) => {
        this.accounts = (accounts || []).filter(a => a.isLettrable);
      },
      error: (err) => {
        this.actionError = this.errorText(err) || this.i18n.t('lettrage.accountsLoadError');
      }
    });
    this.loadGroups();
  }

  onAccountChange(): void {
    this.selectedIds.clear();
    this.autoSummaries = [];
    this.actionError = '';
    this.actionMessage = '';
    this.loadUnlettered();
    this.loadGroups();
  }

  loadUnlettered(): void {
    if (!this.selectedAccount) {
      this.unlettered = [];
      return;
    }
    this.loading = true;
    this.lettrage.getUnlettered(this.selectedAccount).subscribe({
      next: (lines) => {
        let balance = 0;
        this.unlettered = (lines || []).map(l => {
          const debit = +l.debit || 0;
          const credit = +l.credit || 0;
          balance += debit - credit;
          return { ...l, debit, credit, runningBalance: balance };
        });
        this.loading = false;
      },
      error: (err) => {
        this.unlettered = [];
        this.loading = false;
        this.actionError = this.errorText(err) || this.i18n.t('lettrage.loadError');
      }
    });
  }

  loadGroups(): void {
    this.groupsLoading = true;
    this.lettrage.getGroups(this.selectedAccount || undefined).subscribe({
      next: (groups) => {
        this.groups = groups || [];
        this.groupsLoading = false;
      },
      error: (err) => {
        this.groups = [];
        this.groupsLoading = false;
        this.actionError = this.errorText(err) || this.i18n.t('lettrage.loadError');
      }
    });
  }

  refresh(): void {
    this.autoSummaries = [];
    this.loadUnlettered();
    this.loadGroups();
  }

  isSelected(line: LettrageLine): boolean {
    return this.selectedIds.has(line.lineId);
  }

  toggle(line: LettrageLine, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    if (checked) this.selectedIds.add(line.lineId);
    else this.selectedIds.delete(line.lineId);
  }

  get allSelected(): boolean {
    return this.unlettered.length > 0 && this.unlettered.every(l => this.selectedIds.has(l.lineId));
  }

  toggleAll(event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.selectedIds.clear();
    if (checked) this.unlettered.forEach(l => this.selectedIds.add(l.lineId));
  }

  get selectedLines(): LettrageLineView[] {
    return this.unlettered.filter(l => this.selectedIds.has(l.lineId));
  }

  get selectedDebit(): number {
    return this.selectedLines.reduce((s, l) => s + l.debit, 0);
  }

  get selectedCredit(): number {
    return this.selectedLines.reduce((s, l) => s + l.credit, 0);
  }

  get selectionDelta(): number {
    return this.selectedDebit - this.selectedCredit;
  }

  get selectionBalanced(): boolean {
    return Math.abs(this.selectionDelta) <= 0.01;
  }

  get canLetterSelection(): boolean {
    return this.canValidate && !this.acting && this.selectedIds.size >= 2 && this.selectionBalanced;
  }

  letterSelection(): void {
    if (!this.canLetterSelection) return;
    this.acting = true;
    this.actionError = '';
    this.actionMessage = '';
    this.autoSummaries = [];
    this.lettrage.manual([...this.selectedIds]).subscribe({
      next: (res) => {
        this.acting = false;
        this.selectedIds.clear();
        this.actionMessage = this.i18n.t('lettrage.lettered', { code: res.code });
        this.loadUnlettered();
        this.loadGroups();
      },
      error: (err) => {
        this.acting = false;
        this.actionError = this.errorText(err) || this.i18n.t('lettrage.actionError');
      }
    });
  }

  /** Lettrage automatique : compte sélectionné, ou comptes clients + fournisseurs des paramètres si aucun. */
  runAutomatic(): void {
    if (!this.canValidate || this.acting) return;
    this.acting = true;
    this.actionError = '';
    this.actionMessage = '';
    this.lettrage.automatic(this.selectedAccount || undefined).subscribe({
      next: (summaries) => {
        this.acting = false;
        this.autoSummaries = summaries || [];
        const created = this.autoSummaries.reduce((s, x) => s + (+x.groupsCreated || 0), 0);
        this.actionMessage = created > 0
          ? this.i18n.t('lettrage.auto.done', { count: created })
          : this.i18n.t('lettrage.auto.none');
        this.selectedIds.clear();
        this.loadUnlettered();
        this.loadGroups();
      },
      error: (err) => {
        this.acting = false;
        this.actionError = this.errorText(err) || this.i18n.t('lettrage.actionError');
      }
    });
  }

  deletter(group: LettrageGroup): void {
    if (!this.canValidate || this.acting) return;
    if (!confirm(this.i18n.t('lettrage.confirmDeletter', { code: group.code }))) return;
    this.acting = true;
    this.actionError = '';
    this.actionMessage = '';
    this.lettrage.deletter(group.code).subscribe({
      next: (res) => {
        this.acting = false;
        this.actionMessage = this.i18n.t('lettrage.delettered', { code: group.code, count: res.delettered });
        this.loadUnlettered();
        this.loadGroups();
      },
      error: (err) => {
        this.acting = false;
        this.actionError = this.errorText(err) || this.i18n.t('lettrage.actionError');
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
