import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MaterialModule } from '../../material.module';
import { BusinessService } from '../../services/business.service';
import { CashOperation, CashSession } from '../../models/business';
import { PermissionService } from '../../services/permission.service';
import { Permissions } from '../../constants/permissions';
import { AppI18nService } from '../../services/app-i18n.service';
import { TPipe } from '../../pipes/t.pipe';
import { FormHelpComponent } from '../shared/form-help/form-help.component';
import { TableSortState } from '../../utils/table-sort';
import { SortableThComponent } from '../shared/sortable-th/sortable-th.component';

@Component({
  selector: 'app-cash-register',
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, TPipe, FormHelpComponent, SortableThComponent],
  templateUrl: './cash-register.component.html',
  styleUrls: ['./cash-register.component.css']
})
export class CashRegisterComponent implements OnInit {
  selectedTab: 0 | 1 = 0;
  activeSession: CashSession | null = null;
  sessionHistory: CashSession[] = [];
  selectedHistorySession: CashSession | null = null;
  operationSort = new TableSortState('createdAt', 'desc');
  sessionSort = new TableSortState('openedAt', 'desc');
  openingBalanceInput = 50.0;
  closingBalanceInput = 0;
  showOpenModal = false;
  showCloseModal = false;
  showOpModal = false;
  loading = false;
  historyLoading = false;
  actionMessage = '';
  actionError = '';

  opType: 'Deposit' | 'Withdrawal' | 'SalePayment' = 'Deposit';
  opAmount = 0;
  opDescription = '';
  opReference = '';
  readonly P = Permissions;

  constructor(
    private businessService: BusinessService,
    public perm: PermissionService,
    private i18n: AppI18nService
  ) {}

  ngOnInit(): void {
    this.loadSession();
    this.loadHistory();
  }

  selectTab(tab: 0 | 1): void {
    this.selectedTab = tab;
    if (tab === 1 && this.sessionHistory.length === 0) {
      this.loadHistory();
    }
  }

  loadSession(): void {
    this.loading = true;
    this.businessService.getActiveCashSession().subscribe({
      next: (session) => {
        this.activeSession = session ? this.normalizeSession(session) : null;
        this.loading = false;
      },
      error: () => {
        this.activeSession = null;
        this.loading = false;
      }
    });
  }

  loadHistory(): void {
    this.historyLoading = true;
    this.businessService.getCashSessions(50).subscribe({
      next: (sessions) => {
        this.sessionHistory = (sessions || []).map(s => this.normalizeSession(s));
        this.historyLoading = false;
      },
      error: () => {
        this.sessionHistory = [];
        this.historyLoading = false;
      }
    });
  }

  viewHistorySession(session: CashSession): void {
    if (!session.id) return;
    this.businessService.getCashSessionById(session.id).subscribe({
      next: (full) => {
        this.selectedHistorySession = this.normalizeSession(full);
      },
      error: (error) => {
        this.actionError = error?.error?.error || error?.error || this.i18n.t('cash.loadSessionError');
      }
    });
  }

  openSession(): void {
    this.actionError = '';
    this.businessService.openCashSession(this.openingBalanceInput).subscribe({
      next: () => {
        this.showOpenModal = false;
        this.actionMessage = this.i18n.t('cash.opened');
        this.loadSession();
        this.loadHistory();
      },
      error: (error) => {
        this.actionError = error?.error?.error || error?.error || this.i18n.t('cash.openError');
      }
    });
  }

  openCloseModal(): void {
    this.closingBalanceInput = this.theoreticalBalance;
    this.showCloseModal = true;
  }

  closeSession(): void {
    if (!this.activeSession?.id) return;
    this.actionError = '';
    this.businessService.closeCashSession(this.activeSession.id, this.closingBalanceInput).subscribe({
      next: (closed) => {
        this.showCloseModal = false;
        const expected = closed.expectedClosingBalance ?? this.theoreticalBalance;
        const diff = (closed.closingBalance ?? this.closingBalanceInput) - expected;
        this.actionMessage = this.i18n.t('cash.closed', { diff: diff.toFixed(2) });
        this.loadSession();
        this.loadHistory();
      },
      error: (error) => {
        this.actionError = error?.error?.error || error?.error || this.i18n.t('cash.closeError');
      }
    });
  }

  addOperation(): void {
    if (!this.activeSession?.id || this.opAmount <= 0) {
      this.actionError = this.i18n.t('cash.invalidAmount');
      return;
    }

    this.actionError = '';
    this.businessService.postCashOperation({
      cashSessionId: this.activeSession.id,
      operationType: this.opType,
      amount: this.opAmount,
      description: this.opDescription,
      referenceDocument: this.opReference || undefined
    }).subscribe({
      next: () => {
        this.showOpModal = false;
        this.opAmount = 0;
        this.opDescription = '';
        this.opReference = '';
        this.actionMessage = this.i18n.t('cash.opSaved');
        this.loadSession();
      },
      error: (error) => {
        this.actionError = error?.error?.error || error?.error || this.i18n.t('cash.opSaveError');
      }
    });
  }

  get operations(): CashOperation[] {
    return this.activeSession?.operations || [];
  }

  get sortedOperations(): CashOperation[] {
    void this.operationSort.version;
    return this.operationSort.sort(this.operations, {
      createdAt: o => o.createdAt ?? '',
      operationType: o => o.operationType ?? '',
      description: o => o.description ?? '',
      referenceDocument: o => o.referenceDocument ?? '',
      amount: o => o.amount ?? 0,
      createdBy: o => o.createdBy ?? ''
    });
  }

  get sortedSessionHistory(): CashSession[] {
    void this.sessionSort.version;
    return this.sessionSort.sort(this.sessionHistory, {
      sessionNumber: s => s.sessionNumber ?? '',
      openedAt: s => s.openedAt ?? '',
      status: s => s.status ?? '',
      openingBalance: s => s.openingBalance ?? 0,
      closingBalance: s => s.closingBalance ?? null,
      variance: s => this.sessionVariance(s)
    });
  }

  get historyOperations(): CashOperation[] {
    return this.selectedHistorySession?.operations || [];
  }

  get depositsTotal(): number {
    return this.sumInflows(this.operations);
  }

  get withdrawalsTotal(): number {
    return this.sumOutflows(this.operations);
  }

  get theoreticalBalance(): number {
    if (!this.activeSession) return 0;
    return (this.activeSession.openingBalance || 0) + this.depositsTotal - this.withdrawalsTotal;
  }

  get closeDifference(): number {
    return this.closingBalanceInput - this.theoreticalBalance;
  }

  sessionVariance(session: CashSession): number | null {
    if (session.closingBalance == null || session.expectedClosingBalance == null) return null;
    return session.closingBalance - session.expectedClosingBalance;
  }

  operationLabel(type: string): string {
    switch (type) {
      case 'Deposit': return this.i18n.t('cash.opLabel.deposit');
      case 'Withdrawal': return this.i18n.t('cash.opLabel.withdrawal');
      case 'SalePayment': return this.i18n.t('cash.opLabel.salePayment');
      default: return type;
    }
  }

  private sumInflows(ops: CashOperation[]): number {
    return ops
      .filter(o => o.operationType === 'Deposit' || o.operationType === 'SalePayment')
      .reduce((sum, o) => sum + (o.amount || 0), 0);
  }

  private sumOutflows(ops: CashOperation[]): number {
    return ops
      .filter(o => o.operationType === 'Withdrawal')
      .reduce((sum, o) => sum + (o.amount || 0), 0);
  }

  private normalizeSession(session: CashSession): CashSession {
    return { ...session, operations: [...(session.operations || [])] };
  }
}
