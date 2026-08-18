import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MaterialModule } from '../../../material.module';
import { AccountingChartService } from '../../../services/accounting-chart.service';
import { AccountingReportsService } from '../../../services/accounting-reports.service';
import { BalanceReport, ChartOfAccount, LedgerReport } from '../../../models/accounting';
import { AppI18nService } from '../../../services/app-i18n.service';
import { TPipe } from '../../../pipes/t.pipe';
import { FormHelpComponent } from '../../shared/form-help/form-help.component';

type ReportTab = 'balance' | 'ledger';

@Component({
  selector: 'app-accounting-reports',
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, TPipe, FormHelpComponent],
  templateUrl: './accounting-reports.component.html',
  styleUrls: ['./accounting-reports.component.css']
})
export class AccountingReportsComponent implements OnInit {
  tab: ReportTab = 'balance';
  from = '';
  to = '';
  search = '';
  selectedAccount = '';
  accounts: ChartOfAccount[] = [];

  balance: BalanceReport | null = null;
  ledger: LedgerReport | null = null;
  loading = false;
  actionError = '';

  constructor(
    private reports: AccountingReportsService,
    private chart: AccountingChartService,
    private i18n: AppI18nService
  ) {}

  ngOnInit(): void {
    const today = new Date();
    this.from = this.toIsoDate(new Date(today.getFullYear(), 0, 1));
    this.to = this.toIsoDate(today);
    this.chart.getAccounts().subscribe({
      next: (accounts) => this.accounts = accounts,
      error: () => { /* le filtre compte reste saisiable à la main */ }
    });
    this.chart.getFiscalYears().subscribe({
      next: (years) => {
        const open = years.find(y => y.status === 'Open');
        if (open) {
          this.from = (open.startDate || '').slice(0, 10) || this.from;
          const end = (open.endDate || '').slice(0, 10);
          const today = this.to;
          this.to = end && end < today ? end : today;
        }
        this.loadBalance();
      },
      error: () => this.loadBalance()
    });
  }

  get filteredBalanceRows() {
    const rows = this.balance?.rows ?? [];
    const q = this.search.trim().toLowerCase();
    if (!q) return rows;
    return rows.filter(r =>
      r.accountCode.toLowerCase().includes(q) ||
      (r.accountLabel || '').toLowerCase().includes(q));
  }

  setTab(tab: ReportTab): void {
    this.tab = tab;
    if (tab === 'balance' && !this.balance) this.loadBalance();
    if (tab === 'ledger' && this.selectedAccount && !this.ledger) this.loadLedger();
  }

  refresh(): void {
    if (this.tab === 'ledger') this.loadLedger();
    else this.loadBalance();
  }

  openLedger(accountCode: string): void {
    this.selectedAccount = accountCode;
    if (!this.accounts.some(a => a.accountNumber === accountCode)) {
      const row = this.balance?.rows.find(r => r.accountCode === accountCode);
      this.accounts = [
        {
          id: 0,
          accountNumber: accountCode,
          label: row?.accountLabel || accountCode,
          accountClass: 0,
          accountType: '',
          isLettrable: false,
          isBilan: false,
          isResultat: false
        },
        ...this.accounts
      ];
    }
    this.tab = 'ledger';
    this.loadLedger();
  }

  loadBalance(): void {
    this.loading = true;
    this.actionError = '';
    this.reports.getBalance(this.from || undefined, this.to || undefined).subscribe({
      next: (report) => {
        this.balance = report;
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        this.actionError = this.errorText(err) || this.i18n.t('reports.loadError');
      }
    });
  }

  loadLedger(): void {
    if (!this.selectedAccount) {
      this.ledger = null;
      return;
    }
    this.loading = true;
    this.actionError = '';
    this.reports.getGeneralLedger(
      this.selectedAccount,
      this.from || undefined,
      this.to || undefined
    ).subscribe({
      next: (report) => {
        this.ledger = report;
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        this.actionError = this.errorText(err) || this.i18n.t('reports.loadError');
      }
    });
  }

  private toIsoDate(d: Date): string {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }

  private errorText(err: unknown): string {
    const e = err as { error?: unknown };
    if (typeof e?.error === 'string') return e.error;
    const obj = e?.error as { error?: string; message?: string } | undefined;
    return obj?.error || obj?.message || '';
  }
}
