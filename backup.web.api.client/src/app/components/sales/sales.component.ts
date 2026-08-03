import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MaterialModule } from '../../material.module';
import { BusinessService } from '../../services/business.service';
import { downloadBlob } from '../../utils/download-blob.util';
import {
  CreditNote,
  Customer,
  Quote,
  QuoteLine,
  SalesDeliveryNote,
  SalesDeliveryNoteLine,
  SalesInvoice,
  SalesInvoiceLine,
  SalesOrder,
  SalesOrderLine,
  Payment,
  SalesPilotage,
  DocumentAuditLog,
  SalesTrashItem,
  SalesReturn,
  Proforma,
  DepositInvoice
} from '../../models/business';
import { Observable, forkJoin } from 'rxjs';
import { ProductLineRefComponent } from '../shared/product-line-ref/product-line-ref.component';
import { PermissionService } from '../../services/permission.service';
import { Permissions } from '../../constants/permissions';
import { HasPermissionDirective } from '../../directives/has-permission.directive';
import { AppI18nService } from '../../services/app-i18n.service';
import { TPipe } from '../../pipes/t.pipe';
import { FormHelpComponent } from '../shared/form-help/form-help.component';
import { FieldHelpComponent } from '../shared/field-help/field-help.component';
import { HelpAlertsComponent } from '../shared/help-alerts/help-alerts.component';
import { HelpWalkthroughComponent } from '../shared/help-walkthrough/help-walkthrough.component';
import { evaluateHelpAlerts, HelpAlert } from '../../services/help-alerts';
import { StockService } from '../../services/stock.service';

type DocKind = 'Quote' | 'Order' | 'Invoice';

interface DocLineDraft {
  id?: number;
  productKey: string;
  description: string;
  quantity: number;
  unitPrice: number;
  vatRate: number;
  totalHT: number;
  totalTTC: number;
  lineNumber: number;
  deliveredQuantity?: number;
  invoicedQuantity?: number;
  locked?: boolean;
  /** RG-CC3 : prix figé après confirmation commande. */
  priceLocked?: boolean;
}

@Component({
  selector: 'app-sales',
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, ProductLineRefComponent, HasPermissionDirective, TPipe, FormHelpComponent, FieldHelpComponent, HelpAlertsComponent, HelpWalkthroughComponent],
  templateUrl: './sales.component.html',
  styleUrls: ['./sales.component.css']
})
export class SalesComponent implements OnInit {
  /**
   * Flux ligne 1: 0 Devis, 1 Commandes, 2 BL, 3 Factures, 4 Avoirs, 5 Clients
   * Annexes ligne 2: 6 BRC, 7 Proformas, 8 Acomptes, 9 Paiements, 10 Pilotage
   */
  selectedTab = 0;
  loading = false;
  saving = false;
  searchQuery = '';
  actionMessage = '';
  actionError = '';

  invoices: SalesInvoice[] = [];
  orders: SalesOrder[] = [];
  quotes: Quote[] = [];
  creditNotes: CreditNote[] = [];
  deliveryNotes: SalesDeliveryNote[] = [];
  customers: Customer[] = [];
  payments: Payment[] = [];
  paymentsLoading = false;
  pdfDownloading = false;
  pilotage: SalesPilotage | null = null;
  pilotageLoading = false;

  // BRC / Proformas / Acomptes (P3)
  salesReturns: SalesReturn[] = [];
  proformas: Proforma[] = [];
  depositInvoices: DepositInvoice[] = [];
  trashItems: SalesTrashItem[] = [];
  trashLoading = false;
  trashRestoringKey: string | null = null;
  trashBusy = false;

  showReturnModal = false;
  returnDeliveryNoteId: number | null = null;
  returnNotes = '';
  returnDnSearch = '';
  returnDnLoading = false;

  showProformaModal = false;
  proformaQuoteId: number | null = null;

  showDepositModal = false;
  depositOrderId: number | null = null;
  depositAmountHT = 0;
  depositVatRate = 21;

  showApplyDepositModal = false;
  depositToApply: DepositInvoice | null = null;
  applyDepositInvoiceId: number | null = null;

  showDocModal = false;
  docKind: DocKind = 'Invoice';
  docCustomerId: number | null = null;
  docNotes = '';
  docExpirationDate = '';
  docDueDate = '';
  docLines: DocLineDraft[] = [];
  docSalesOrderId: number | null = null;
  docSalesDeliveryNoteId: number | null = null;
  /** RG-BL7 : multi-BL → une facture. */
  docSalesDeliveryNoteIds: number[] = [];
  docSourceLoading = false;
  editingOrderId: number | null = null;
  editingInvoiceId: number | null = null;
  /** Commande post-confirm : client + prix figés. */
  editingOrderCommitted = false;

  showCustomerModal = false;
  editingCustomerId: number | null = null;
  newCustomer: Partial<Customer> = this.emptyCustomer();
  openCustomerFromDoc = false;

  showPaymentModal = false;
  selectedInvoiceForPayment: SalesInvoice | null = null;
  paymentAmount = 0;
  paymentMethod = 'Cash';
  paymentReference = '';
  paymentBank = '';

  showCreditNoteModal = false;
  creditNoteInvoice: SalesInvoice | null = null;
  creditNoteNotes = '';
  creditNoteLines: {
    invoiceLineId?: number;
    lineNumber: number;
    productKey: string;
    description: string;
    maxQuantity: number;
    unitPrice: number;
    vatRate: number;
    selected: boolean;
    quantity: number;
  }[] = [];
  creditNoteLoading = false;

  expandedRowKey: string | null = null;
  detailLoading = false;
  detailKind: 'Quote' | 'Order' | 'Invoice' | 'CreditNote' | 'DeliveryNote' | null = null;
  detailQuote: Quote | null = null;
  detailOrder: SalesOrder | null = null;
  detailInvoice: SalesInvoice | null = null;
  detailCreditNote: CreditNote | null = null;
  detailDeliveryNote: SalesDeliveryNote | null = null;
  detailAudit: DocumentAuditLog[] = [];
  detailAuditLoading = false;

  // BL creation form
  showDeliveryNoteModal = false;
  dnCustomerId: number | null = null;
  dnSalesOrderId: number | null = null;
  dnDate = '';
  dnNotes = '';
  dnLines: { productKey: string; description: string; orderedQuantity: number; deliveredQuantity: number; unitPrice: number; vatRate: number; totalHT: number; totalTTC: number; lineNumber: number }[] = [];

  readonly P = Permissions;

  docHelpAlerts: HelpAlert[] = [];
  stockByProduct: Record<string, number> = {};
  showWalkthrough = false;
  walkthroughHelpKey = 'sales.order';

  constructor(
    private businessService: BusinessService,
    public perm: PermissionService,
    private i18n: AppI18nService,
    private stockService: StockService
  ) {}

  ngOnInit(): void {
    this.loadAllData();
  }

  get createButtonLabel(): string {
    switch (this.selectedTab) {
      case 0: return this.i18n.t('sales.btn.newQuote');
      case 1: return this.i18n.t('sales.btn.newOrder');
      case 2: return this.i18n.t('sales.btn.newDeliveryNote');
      case 5: return this.i18n.t('sales.btn.newCustomer');
      default: return this.i18n.t('sales.btn.newInvoice');
    }
  }

  /** Aide F1 / panneau ? selon l’onglet actif. */
  get activeTabHelpKey(): string {
    const keys = [
      'sales.quote',
      'sales.order',
      'sales.deliveryNote',
      'sales.invoice',
      'sales.creditNote',
      'sales.customer',
      'sales.return',
      'sales.proforma',
      'sales.deposit',
      'sales.payment',
      'sales.pilotage',
      'sales.trash'
    ];
    return keys[this.selectedTab] || 'sales.tabs';
  }

  get activeTabHelpAbbrs(): string[] {
    switch (this.selectedTab) {
      case 2: return ['BL', 'BLC', 'CMD'];
      case 3: return ['FAC', 'HT', 'TTC', 'TVA', 'BL'];
      case 4: return ['FAC', 'HT', 'TVA'];
      case 6: return ['BRC', 'BL', 'FAC'];
      case 7: return ['FAC', 'HT', 'TVA'];
      case 8: return ['FAC', 'TTC'];
      case 9: return ['FAC', 'TTC'];
      default: return ['BL', 'BLC', 'BRC', 'FAC', 'HT', 'TTC', 'TVA'];
    }
  }

  get docModalTitle(): string {
    if (this.docKind === 'Order' && this.editingOrderId) return this.i18n.t('sales.modal.editOrder');
    if (this.docKind === 'Invoice' && this.editingInvoiceId) return this.i18n.t('sales.modal.editInvoice');
    switch (this.docKind) {
      case 'Quote': return this.i18n.t('sales.modal.newQuote');
      case 'Order': return this.i18n.t('sales.modal.newOrder');
      default: return this.i18n.t('sales.modal.newInvoice');
    }
  }

  get docHelpKey(): string {
    switch (this.docKind) {
      case 'Quote': return 'sales.quote';
      case 'Order': return 'sales.order';
      default: return 'sales.invoice';
    }
  }

  get docStatusForHelp(): string | null {
    if (this.docKind === 'Order' && this.editingOrderId) {
      const o = this.orders.find(x => x.id === this.editingOrderId);
      return o?.status || 'Draft';
    }
    if (this.docKind === 'Invoice' && this.editingInvoiceId) {
      const inv = this.invoices.find(x => x.id === this.editingInvoiceId);
      return inv?.status || 'Draft';
    }
    if (this.docKind === 'Quote') {
      // editing quote if any
      return 'Draft';
    }
    return 'Draft';
  }

  get docHasBlockingAlert(): boolean {
    return this.docHelpAlerts.some(a => a.severity === 'block');
  }

  refreshDocHelpAlerts(): void {
    const customer = this.customers.find(c => c.id === this.docCustomerId) || null;
    const openOrdersTtc = this.orders
      .filter(o => o.customerId === this.docCustomerId
        && o.status !== 'Cancelled' && o.status !== 'Draft' && o.status !== 'Closed'
        && o.id !== this.editingOrderId)
      .reduce((s, o) => s + (o.totalTTC || 0), 0);

    this.docHelpAlerts = evaluateHelpAlerts({
      customer,
      openOrdersTtc,
      documentTtc: this.docTotals.ttc,
      documentKind: this.docKind,
      lines: this.docLines.map(l => ({
        productKey: l.productKey,
        quantity: l.quantity,
        unitPrice: l.unitPrice,
        vatRate: l.vatRate
      })),
      stockByProduct: this.stockByProduct,
      expectedVatRate: 21
    }, (key, p) => this.i18n.t(key, p));
  }

  loadStockForDocLines(): void {
    if (!this.perm.has(Permissions.StockRead)) {
      this.refreshDocHelpAlerts();
      return;
    }
    this.stockService.getAll().subscribe({
      next: items => {
        const map: Record<string, number> = {};
        for (const it of items || []) {
          if (it.productKey) map[it.productKey] = it.quantityOnHand ?? 0;
        }
        this.stockByProduct = map;
        this.refreshDocHelpAlerts();
      },
      error: () => this.refreshDocHelpAlerts()
    });
  }

  onWalkthrough(helpKey: string): void {
    this.walkthroughHelpKey = helpKey;
    this.showWalkthrough = true;
  }

  get docTotals(): { ht: number; vat: number; ttc: number } {
    const ht = this.docLines.reduce((sum, l) => sum + (l.totalHT || 0), 0);
    const vat = this.docLines.reduce((sum, l) => sum + (l.totalHT || 0) * ((l.vatRate || 0) / 100), 0);
    return { ht, vat, ttc: ht + vat };
  }

  canCreateOnTab(): boolean {
    switch (this.selectedTab) {
      case 0: return this.perm.has(Permissions.QuoteCreate);
      case 1: return this.perm.has(Permissions.OrderCreate);
      case 2: return this.perm.has(Permissions.DeliveryNoteCreate);
      case 3: return this.perm.has(Permissions.InvoiceCreate);
      case 5: return this.perm.has(Permissions.CustomerCreate);
      default: return false;
    }
  }

  loadAllData(): void {
    this.loading = true;
    this.businessService.getCustomers().subscribe(c => this.customers = c);
    this.businessService.getQuotes().subscribe(q => this.quotes = q);
    this.businessService.getSalesOrders().subscribe(o => this.orders = o);
    this.businessService.getCreditNotes().subscribe(cn => this.creditNotes = cn);
    this.businessService.getSalesDeliveryNotes().subscribe(d => this.deliveryNotes = d);
    this.loadPayments();
    this.loadPilotage();
    this.loadReturns();
    this.loadProformas();
    this.loadDepositInvoices();
    this.loadTrash();
    this.businessService.getSalesInvoices().subscribe(i => {
      this.invoices = i;
      this.loading = false;
    });
  }

  /**
   * Après une opération popup réussie : ferme le modal et recharge
   * les listes principales (+ détail ouvert si besoin).
   */
  private finishModalSuccess(close: () => void, message?: string): void {
    close();
    if (message) this.actionMessage = message;
    this.refreshMainAfterModal();
  }

  private refreshMainAfterModal(): void {
    this.loadAllData();
    this.reloadExpandedDetail();
  }

  private reloadExpandedDetail(): void {
    const key = this.expandedRowKey;
    if (!key || !this.detailKind) return;
    const id = Number(key.split(':')[1]);
    if (!id) return;
    const kind = this.detailKind;
    this.detailLoading = true;
    const done = () => { this.detailLoading = false; };
    if (kind === 'Order') {
      this.businessService.getSalesOrder(id).subscribe({
        next: (full) => { this.detailOrder = full; done(); this.loadDocumentAudit('Order', id); },
        error: () => { this.expandedRowKey = null; this.resetDetail(); done(); }
      });
    } else if (kind === 'Invoice') {
      this.businessService.getSalesInvoice(id).subscribe({
        next: (full) => {
          this.detailInvoice = full;
          this.syncInvoiceListRow(full);
          done();
          this.loadDocumentAudit('Invoice', id);
        },
        error: () => { this.expandedRowKey = null; this.resetDetail(); done(); }
      });
    } else if (kind === 'CreditNote') {
      this.businessService.getCreditNote(id).subscribe({
        next: (full) => { this.detailCreditNote = full; done(); },
        error: () => { this.expandedRowKey = null; this.resetDetail(); done(); }
      });
    } else if (kind === 'DeliveryNote') {
      this.businessService.getSalesDeliveryNote(id).subscribe({
        next: (full) => { this.detailDeliveryNote = full; done(); },
        error: () => { this.expandedRowKey = null; this.resetDetail(); done(); }
      });
    } else if (kind === 'Quote') {
      this.businessService.getQuote(id).subscribe({
        next: (full) => { this.detailQuote = full; done(); },
        error: () => { this.expandedRowKey = null; this.resetDetail(); done(); }
      });
    } else {
      done();
    }
  }

  loadTrash(): void {
    if (!this.perm.has(Permissions.InvoiceRead)
      && !this.perm.has(Permissions.OrderRead)
      && !this.perm.has(Permissions.DeliveryNoteRead)
      && !this.perm.has(Permissions.QuoteRead)) {
      this.trashItems = [];
      return;
    }
    this.trashLoading = true;
    this.businessService.getSalesTrash(this.selectedTab === 11 ? (this.searchQuery || undefined) : undefined).subscribe({
      next: (items) => {
        this.trashItems = items || [];
        this.trashLoading = false;
      },
      error: () => {
        this.trashItems = [];
        this.trashLoading = false;
      }
    });
  }

  loadReturns(): void {
    if (!this.perm.has(Permissions.SalesReturnRead)) {
      this.salesReturns = [];
      return;
    }
    this.businessService.getSalesReturns().subscribe({
      next: (r) => this.salesReturns = r,
      error: () => this.salesReturns = []
    });
  }

  loadProformas(): void {
    if (!this.perm.has(Permissions.InvoiceRead)) {
      this.proformas = [];
      return;
    }
    this.businessService.getProformas().subscribe({
      next: (p) => this.proformas = p,
      error: () => this.proformas = []
    });
  }

  loadDepositInvoices(): void {
    if (!this.perm.has(Permissions.InvoiceRead)) {
      this.depositInvoices = [];
      return;
    }
    this.businessService.getDepositInvoices().subscribe({
      next: (d) => this.depositInvoices = d,
      error: () => this.depositInvoices = []
    });
  }

  loadPilotage(): void {
    if (!this.perm.has(Permissions.OrderRead)) {
      this.pilotage = null;
      return;
    }
    this.pilotageLoading = true;
    this.businessService.getSalesPilotage().subscribe({
      next: (p) => {
        this.pilotage = p;
        this.pilotageLoading = false;
      },
      error: () => {
        this.pilotage = null;
        this.pilotageLoading = false;
      }
    });
  }

  loadPayments(): void {
    if (!this.perm.has(Permissions.InvoiceRead)) {
      this.payments = [];
      return;
    }
    this.paymentsLoading = true;
    this.businessService.getPayments().subscribe({
      next: (p) => {
        this.payments = p || [];
        this.paymentsLoading = false;
      },
      error: () => {
        this.payments = [];
        this.paymentsLoading = false;
      }
    });
  }

  onSearch(): void {
    if (this.selectedTab === 0) {
      this.businessService.getQuotes(this.searchQuery || undefined).subscribe(res => this.quotes = res);
    } else if (this.selectedTab === 1) {
      this.businessService.getSalesOrders(this.searchQuery || undefined).subscribe(res => this.orders = res);
    } else if (this.selectedTab === 2) {
      this.businessService.getSalesDeliveryNotes(this.searchQuery || undefined).subscribe(res => this.deliveryNotes = res);
    } else if (this.selectedTab === 3) {
      this.businessService.getSalesInvoices(this.searchQuery || undefined).subscribe(res => this.invoices = res);
    } else if (this.selectedTab === 4) {
      this.businessService.getCreditNotes(this.searchQuery || undefined).subscribe(res => this.creditNotes = res);
    } else if (this.selectedTab === 5) {
      this.businessService.getCustomers(this.searchQuery || undefined).subscribe(res => this.customers = res);
    } else if (this.selectedTab === 6) {
      if (this.searchQuery) {
        this.businessService.getSalesReturns(this.searchQuery).subscribe(res => this.salesReturns = res);
      } else {
        this.loadReturns();
      }
    } else if (this.selectedTab === 7) {
      if (this.searchQuery) {
        this.businessService.getProformas(this.searchQuery).subscribe(res => this.proformas = res);
      } else {
        this.loadProformas();
      }
    } else if (this.selectedTab === 8) {
      if (this.searchQuery) {
        this.businessService.getDepositInvoices(this.searchQuery).subscribe(res => this.depositInvoices = res);
      } else {
        this.loadDepositInvoices();
      }
    } else if (this.selectedTab === 9) {
      this.loadPayments();
    } else if (this.selectedTab === 10) {
      this.loadPilotage();
    } else if (this.selectedTab === 11) {
      this.loadTrash();
    }
  }

  onCreateClick(): void {
    if (this.selectedTab === 5) {
      this.openCustomerModal();
      return;
    }
    if (this.selectedTab === 2) {
      this.openDeliveryNoteModal();
      return;
    }
    if (this.customers.length === 0) {
      this.actionError = this.i18n.t('sales.needCustomerFirst');
      this.selectedTab = 5;
      return;
    }
    if (this.selectedTab === 0) this.openDocModal('Quote');
    else if (this.selectedTab === 1) this.openDocModal('Order');
    else this.openDocModal('Invoice');
  }

  downloadPdfFromList(kind: 'Quote' | 'Order' | 'Invoice' | 'CreditNote', id?: number, fallbackName?: string): void {
    if (!id) return;
    this.downloadPdf(kind, id, fallbackName || 'document.pdf');
  }

  downloadCurrentDetailPdf(): void {
    if (this.detailKind === 'Quote' && this.detailQuote?.id) {
      this.downloadPdf('Quote', this.detailQuote.id, `${this.detailQuote.quoteNumber || 'devis'}.pdf`);
    } else if (this.detailKind === 'Order' && this.detailOrder?.id) {
      this.downloadPdf('Order', this.detailOrder.id, `${this.detailOrder.orderNumber || 'commande'}.pdf`);
    } else if (this.detailKind === 'Invoice' && this.detailInvoice?.id) {
      this.downloadPdf('Invoice', this.detailInvoice.id, `${this.detailInvoice.invoiceNumber || 'facture'}.pdf`);
    } else if (this.detailKind === 'CreditNote' && this.detailCreditNote?.id) {
      this.downloadPdf('CreditNote', this.detailCreditNote.id, `${this.detailCreditNote.creditNoteNumber || 'avoir'}.pdf`);
    }
  }

  private downloadPdf(kind: 'Quote' | 'Order' | 'Invoice' | 'CreditNote', id: number, fileName: string): void {
    this.pdfDownloading = true;
    this.actionError = '';
    let request: Observable<Blob>;
    switch (kind) {
      case 'Quote': request = this.businessService.downloadQuotePdf(id); break;
      case 'Order': request = this.businessService.downloadSalesOrderPdf(id); break;
      case 'Invoice': request = this.businessService.downloadSalesInvoicePdf(id); break;
      default: request = this.businessService.downloadCreditNotePdf(id); break;
    }
    request.subscribe({
      next: (blob) => {
        downloadBlob(blob, fileName);
        this.pdfDownloading = false;
        this.actionMessage = this.i18n.t('sales.pdfDownloaded', { fileName });
      },
      error: () => {
        this.pdfDownloading = false;
        this.actionError = this.i18n.t('sales.pdfError');
      }
    });
  }

  openDocModal(kind: DocKind): void {
    this.docKind = kind;
    this.editingOrderId = null;
    this.editingInvoiceId = null;
    this.editingOrderCommitted = false;
    this.docCustomerId = null;
    this.docNotes = '';
    this.docLines = [this.emptyLine(1)];
    this.docSalesOrderId = null;
    this.docSalesDeliveryNoteId = null;
    this.docSalesDeliveryNoteIds = [];
    this.docSourceLoading = false;
    const today = new Date();
    const inThirty = new Date(today.getTime() + 30 * 24 * 60 * 60 * 1000);
    this.docExpirationDate = this.toDateInput(inThirty);
    this.docDueDate = this.toDateInput(inThirty);
    this.actionError = '';
    this.showDocModal = true;
    this.loadStockForDocLines();
  }

  canEditOrder(order: SalesOrder): boolean {
    if (!this.perm.has(Permissions.OrderUpdate) || !order.id) return false;
    const s = (order.status || '').toLowerCase();
    if (s === 'cancelled' || s === 'closed' || s === 'invoiced') return false;
    return true;
  }

  isOrderCommitted(order: SalesOrder | null | undefined): boolean {
    const s = (order?.status || '').toLowerCase();
    return !!s && s !== 'draft' && s !== 'pending' && s !== 'cancelled';
  }

  /** Qté déjà engagée sur BL non annulés pour une ligne commande. */
  private allocatedQtyOnDns(orderId: number, productKey: string | undefined): number {
    const key = (productKey || '').trim().toLowerCase();
    return this.deliveryNotes
      .filter(dn => dn.salesOrderId === orderId && (dn.status || '').toLowerCase() !== 'cancelled')
      .reduce((sum, dn) => {
        const lineQty = (dn.lines || [])
          .filter(l => (l.productKey || '').trim().toLowerCase() === key)
          .reduce((s, l) => s + Number(l.deliveredQuantity || 0), 0);
        return sum + lineQty;
      }, 0);
  }

  canCancelOrder(order: SalesOrder): boolean {
    if (!this.perm.has(Permissions.OrderUpdate) || !order.id) return false;
    const s = (order.status || '').toLowerCase();
    if (s === 'cancelled' || s === 'closed' || s === 'invoiced') return false;
    const lines = order.lines || [];
    if (lines.some(l => Number(l.deliveredQuantity || 0) > 0 || Number(l.invoicedQuantity || 0) > 0)) return false;
    if (this.deliveryNotes.some(dn => dn.salesOrderId === order.id && (dn.status || '').toLowerCase() !== 'cancelled')) {
      return false;
    }
    return true;
  }

  openEditOrderModal(order: SalesOrder): void {
    if (!order.id || !this.canEditOrder(order)) return;
    this.saving = true;
    this.businessService.getSalesOrder(order.id).subscribe({
      next: (full) => {
        this.saving = false;
        if (!this.canEditOrder(full)) {
          this.actionError = this.i18n.t('sales.orderEditBlocked');
          return;
        }
        const committed = this.isOrderCommitted(full);
        this.docKind = 'Order';
        this.editingOrderId = full.id!;
        this.editingInvoiceId = null;
        this.editingOrderCommitted = committed;
        this.docCustomerId = full.customerId;
        this.docNotes = full.notes || '';
        this.docSalesOrderId = null;
        this.docSalesDeliveryNoteId = null;
        this.docSalesDeliveryNoteIds = [];
        this.docLines = (full.lines || []).map((l, i) => {
          const delivered = Number(l.deliveredQuantity || 0);
          const invoiced = Number(l.invoicedQuantity || 0);
          const allocated = this.allocatedQtyOnDns(full.id!, l.productKey);
          const locked = delivered > 0 || invoiced > 0 || allocated > 0;
          return {
            id: l.id,
            productKey: l.productKey,
            description: l.description,
            quantity: l.quantity,
            unitPrice: l.unitPrice,
            vatRate: l.vatRate,
            totalHT: l.totalHT,
            totalTTC: l.totalTTC,
            lineNumber: i + 1,
            deliveredQuantity: Math.max(delivered, allocated),
            invoicedQuantity: invoiced,
            locked,
            priceLocked: committed || delivered > 0 || invoiced > 0
          };
        });
        if (!this.docLines.length) this.docLines = [this.emptyLine(1)];
        this.actionError = '';
        this.showDocModal = true;
      },
      error: () => {
        this.saving = false;
        this.actionError = this.i18n.t('sales.sourceLoadError');
      }
    });
  }

  canEditInvoice(invoice: SalesInvoice): boolean {
    if (!this.perm.has(Permissions.InvoiceUpdate) || !invoice.id) return false;
    return (invoice.status || '').toLowerCase() === 'draft';
  }

  canDeleteInvoice(invoice: SalesInvoice): boolean {
    if (!this.perm.has(Permissions.InvoiceDelete) || !invoice.id) return false;
    return (invoice.status || '').toLowerCase() === 'draft';
  }

  deleteSalesInvoice(invoice: SalesInvoice): void {
    if (!invoice.id || !this.canDeleteInvoice(invoice)) return;
    if (!confirm(this.i18n.t('sales.confirm.deleteInvoice', { number: invoice.invoiceNumber }))) return;
    this.saving = true;
    this.businessService.deleteSalesInvoice(invoice.id).subscribe({
      next: () => {
        this.saving = false;
        this.actionMessage = this.i18n.t('sales.invoiceDeleted', { number: invoice.invoiceNumber });
        this.invoices = this.invoices.filter(i => i.id !== invoice.id);
        if (this.expandedRowKey === this.rowKey('Invoice', invoice.id!)) {
          this.expandedRowKey = null;
          this.resetDetail();
        }
        this.businessService.getSalesDeliveryNotes().subscribe(d => this.deliveryNotes = d);
        this.businessService.getSalesOrders().subscribe(o => this.orders = o);
      },
      error: (err) => {
        this.saving = false;
        this.actionError = typeof err?.error === 'string' ? err.error : (err?.error?.error || this.i18n.t('sales.deleteError'));
      }
    });
  }

  openEditInvoiceModal(invoice: SalesInvoice): void {
    if (!invoice.id || !this.canEditInvoice(invoice)) return;
    this.saving = true;
    this.businessService.getSalesInvoice(invoice.id).subscribe({
      next: (full) => {
        this.saving = false;
        if (!this.canEditInvoice(full)) {
          this.actionError = this.i18n.t('sales.invoiceEditBlocked');
          return;
        }
        this.docKind = 'Invoice';
        this.editingInvoiceId = full.id!;
        this.editingOrderId = null;
        this.docCustomerId = full.customerId;
        this.docNotes = full.notes || '';
        this.docDueDate = this.toDateInput(full.dueDate ? new Date(full.dueDate) : new Date());
        this.docSalesOrderId = full.salesOrderId ?? null;
        const linkedDns = this.deliveryNotes.filter(dn => dn.salesInvoiceId === full.id);
        this.docSalesDeliveryNoteIds = linkedDns.map(dn => dn.id!).filter(Boolean);
        this.docSalesDeliveryNoteId = this.docSalesDeliveryNoteIds[0] ?? null;
        this.docLines = (full.lines || []).map((l, i) => {
          const delivered = Number(l.deliveredQuantity || 0);
          const locked = delivered > 0;
          return {
            id: l.id,
            productKey: l.productKey,
            description: l.description,
            quantity: l.quantity,
            unitPrice: l.unitPrice,
            vatRate: l.vatRate,
            totalHT: l.totalHT,
            totalTTC: l.totalTTC,
            lineNumber: i + 1,
            deliveredQuantity: delivered,
            invoicedQuantity: 0,
            locked,
            priceLocked: locked
          };
        });
        if (!this.docLines.length) this.docLines = [this.emptyLine(1)];
        this.actionError = '';
        this.showDocModal = true;
      },
      error: () => {
        this.saving = false;
        this.actionError = this.i18n.t('sales.sourceLoadError');
      }
    });
  }

  cancelSalesOrder(order: SalesOrder): void {
    if (!order.id || !this.canCancelOrder(order)) return;
    const reason = prompt(this.i18n.t('sales.cancelReasonPrompt'));
    if (reason == null) return;
    if (!reason.trim()) {
      this.actionError = this.i18n.t('sales.cancelReasonRequired');
      return;
    }
    this.saving = true;
    this.businessService.cancelSalesOrder(order.id, reason.trim()).subscribe({
      next: (updated) => {
        this.saving = false;
        this.actionMessage = this.i18n.t('sales.orderCancelled', { number: updated.orderNumber });
        this.businessService.getSalesOrders().subscribe(o => this.orders = o);
      },
      error: (err) => {
        this.saving = false;
        const msg = typeof err?.error === 'string' ? err.error : (err?.error?.error || err?.error);
        this.actionError = msg || this.i18n.t('sales.genericError');
      }
    });
  }

  /** Commandes disponibles pour facturation (filtre client du popup facture). */
  get linkableOrders(): SalesOrder[] {
    return this.orders.filter(o => {
      if (this.editingInvoiceId && this.docSalesOrderId && o.id === this.docSalesOrderId) return true;
      const s = (o.status || '').toLowerCase();
      if (s === 'cancelled' || s === 'closed' || s === 'draft' || s === 'pending') return false;
      if (this.docCustomerId && o.customerId !== this.docCustomerId) return false;
      if (s === 'invoiced' && !this.orderHasRemainingToDeliver(o)) return false;
      return true;
    });
  }

  /** Commandes avec reliquat à livrer (modal BL). */
  get linkableOrdersForDn(): SalesOrder[] {
    return this.orders.filter(o => {
      const s = (o.status || '').toLowerCase();
      if (s === 'cancelled' || s === 'draft' || s === 'pending') return false;
      if (this.dnCustomerId && o.customerId !== this.dnCustomerId) return false;
      const hasOpenDn = this.deliveryNotes.some(dn =>
        dn.salesOrderId === o.id
        && ['draft', 'sent'].includes((dn.status || '').toLowerCase()));
      if (hasOpenDn) return false;
      return this.orderHasRemainingToDeliver(o);
    });
  }

  remainingQty(line: { quantity?: number; deliveredQuantity?: number }): number {
    return Math.max(0, Number(line.quantity || 0) - Number(line.deliveredQuantity || 0));
  }

  orderRemainingTotal(order: SalesOrder): number {
    return (order.lines || []).reduce((sum, l) => sum + this.remainingQty(l), 0);
  }

  orderHasBackorder(order: SalesOrder): boolean {
    const lines = order.lines || [];
    if (!lines.length) return false;
    const hasRemaining = lines.some(l => this.remainingQty(l) > 0.0001);
    const hasDelivered = lines.some(l => Number(l.deliveredQuantity || 0) > 0.0001);
    return hasRemaining && hasDelivered;
  }

  private orderHasRemainingToDeliver(order: SalesOrder): boolean {
    const lines = order.lines || [];
    if (!lines.length) {
      const s = (order.status || '').toLowerCase();
      return s !== 'closed';
    }
    return lines.some(l => Number(l.quantity || 0) - Number(l.deliveredQuantity || 0) > 0.0001);
  }

  /** BL livrés non encore facturés, pour le popup facture. */
  get linkableDeliveryNotesForInvoice(): SalesDeliveryNote[] {
    return this.deliveryNotes.filter(dn => {
      if (this.editingInvoiceId && dn.salesInvoiceId === this.editingInvoiceId) return true;
      if ((dn.status || '').toLowerCase() !== 'delivered') return false;
      if (dn.salesInvoiceId) return false;
      if (this.docCustomerId && dn.customerId !== this.docCustomerId) return false;
      if (this.docSalesOrderId && dn.salesOrderId !== this.docSalesOrderId) return false;
      return true;
    });
  }

  onDocOrderChange(orderId: number | null): void {
    this.docSalesOrderId = orderId ? Number(orderId) : null;
    this.docSalesDeliveryNoteId = null;
    // Facture : la commande filtre seulement les BL — les lignes viennent du BL.
    if (this.docKind === 'Invoice') {
      if (!this.docSalesOrderId) {
        this.docLines = [this.emptyLine(1)];
        return;
      }
      this.docSourceLoading = true;
      this.businessService.getSalesOrder(this.docSalesOrderId).subscribe({
        next: (order) => {
          this.docCustomerId = order.customerId;
          this.docLines = [this.emptyLine(1)];
          this.docSourceLoading = false;
        },
        error: () => {
          this.docSourceLoading = false;
          this.actionError = this.i18n.t('sales.sourceLoadError');
        }
      });
      return;
    }
    if (!this.docSalesOrderId) {
      this.docLines = [this.emptyLine(1)];
      return;
    }
    this.docSourceLoading = true;
    this.businessService.getSalesOrder(this.docSalesOrderId).subscribe({
      next: (order) => {
        this.docCustomerId = order.customerId;
        this.docLines = (order.lines || []).map((l, i) => ({
          productKey: l.productKey,
          description: l.description,
          quantity: l.quantity,
          unitPrice: l.unitPrice,
          vatRate: l.vatRate,
          totalHT: l.totalHT,
          totalTTC: l.totalTTC,
          lineNumber: i + 1
        }));
        if (!this.docLines.length) this.docLines = [this.emptyLine(1)];
        this.docSourceLoading = false;
      },
      error: () => {
        this.docSourceLoading = false;
        this.actionError = this.i18n.t('sales.sourceLoadError');
      }
    });
  }

  onDocDeliveryNoteChange(noteId: number | null): void {
    this.onDocDeliveryNotesChange(noteId ? [Number(noteId)] : []);
  }

  onDocDeliveryNotesChange(noteIds: number[] | null): void {
    const ids = (noteIds || []).map(Number).filter(id => id > 0);
    this.docSalesDeliveryNoteIds = ids;
    this.docSalesDeliveryNoteId = ids.length ? ids[0] : null;
    if (!ids.length) {
      this.docLines = [this.emptyLine(1)];
      return;
    }
    this.docSourceLoading = true;
    this.actionError = '';
    forkJoin(ids.map(id => this.businessService.getSalesDeliveryNote(id))).subscribe({
      next: (notes) => {
        const customerIds = [...new Set(notes.map(n => n.customerId))];
        if (customerIds.length > 1) {
          this.actionError = this.i18n.t('sales.multiBlSameCustomer');
          this.docSourceLoading = false;
          return;
        }
        const first = notes[0];
        this.docCustomerId = first.customerId;
        this.docSalesOrderId = first.salesOrderId ?? null;
        this.applyDueDateFromCustomer(first.customerId);
        const map = new Map<string, DocLineDraft>();
        let lineNo = 0;
        for (const note of notes) {
          for (const l of note.lines || []) {
            if (Number(l.deliveredQuantity || 0) <= 0) continue;
            const key = (l.productKey || '').trim().toLowerCase() || `__${l.description}`;
            const existing = map.get(key);
            if (!existing) {
              lineNo++;
              map.set(key, {
                productKey: l.productKey,
                description: l.description,
                quantity: Number(l.deliveredQuantity || 0),
                unitPrice: l.unitPrice,
                vatRate: l.vatRate,
                totalHT: 0,
                totalTTC: 0,
                lineNumber: lineNo,
                deliveredQuantity: Number(l.deliveredQuantity || 0),
                locked: true,
                priceLocked: true
              });
            } else {
              existing.quantity += Number(l.deliveredQuantity || 0);
              existing.deliveredQuantity = (existing.deliveredQuantity || 0) + Number(l.deliveredQuantity || 0);
            }
          }
        }
        this.docLines = [...map.values()];
        this.docLines.forEach(l => this.calcLine(l));
        if (!this.docLines.length) this.docLines = [this.emptyLine(1)];
        this.docSourceLoading = false;
      },
      error: () => {
        this.docSourceLoading = false;
        this.actionError = this.i18n.t('sales.sourceLoadError');
      }
    });
  }

  /** RG-EC1 : propose l'échéance selon PaymentTerms du client. */
  applyDueDateFromCustomer(customerId: number | null): void {
    if (!customerId) return;
    const customer = this.customers.find(c => c.id === customerId);
    const days = this.parsePaymentTermsDays(customer?.paymentTerms) ?? 30;
    const base = new Date();
    base.setHours(0, 0, 0, 0);
    base.setDate(base.getDate() + days);
    if (this.isPaymentTermsEom(customer?.paymentTerms)) {
      const eom = new Date(base.getFullYear(), base.getMonth() + 1, 0);
      this.docDueDate = this.toDateInput(eom);
      return;
    }
    this.docDueDate = this.toDateInput(base);
  }

  private parsePaymentTermsDays(terms?: string | null): number | null {
    if (!terms) return null;
    const m = terms.match(/(?<!\d)(\d{1,3})\s*(?:j(?:ours?)?|d(?:ays?)?|dagen)?(?!\d)/i);
    return m ? Number(m[1]) : null;
  }

  private isPaymentTermsEom(terms?: string | null): boolean {
    if (!terms) return false;
    const t = terms.toLowerCase();
    return t.includes('eom') || t.includes('fin de mois') || t.includes('einde maand') || t.includes('fdm');
  }

  addDocLine(): void {
    this.docLines = [...this.docLines, this.emptyLine(this.docLines.length + 1)];
  }

  get hasLockedDocLines(): boolean {
    return this.docLines.some(l => !!l.locked);
  }

  removeDocLine(index: number): void {
    const line = this.docLines[index];
    if (line?.locked) {
      this.actionError = this.i18n.t('sales.orderLineLocked');
      return;
    }
    this.docLines = this.docLines.filter((_, i) => i !== index);
    this.docLines.forEach((l, i) => l.lineNumber = i + 1);
  }

  calcLine(line: DocLineDraft): void {
    if (line.locked && this.docKind === 'Order') {
      const minQty = Math.max(Number(line.deliveredQuantity || 0), Number(line.invoicedQuantity || 0));
      if (Number(line.quantity || 0) < minQty) line.quantity = minQty;
    }
    if (line.locked && this.docKind === 'Invoice') {
      const maxQty = Number(line.deliveredQuantity || 0);
      if (maxQty > 0 && Number(line.quantity || 0) > maxQty) line.quantity = maxQty;
      if (Number(line.quantity || 0) <= 0) line.quantity = 0.01;
    }
    line.totalHT = +(Number(line.quantity || 0) * Number(line.unitPrice || 0)).toFixed(2);
    line.totalTTC = +(line.totalHT * (1 + Number(line.vatRate || 0) / 100)).toFixed(2);
    this.refreshDocHelpAlerts();
  }

  saveDocument(): void {
    if (!this.docCustomerId) {
      this.actionError = this.i18n.t('sales.selectCustomerError');
      return;
    }
    if (!this.docLines.length || this.docLines.every(l => !l.description && !l.productKey)) {
      this.actionError = this.i18n.t('sales.addLineError');
      return;
    }

    this.refreshDocHelpAlerts();
    if (this.docHasBlockingAlert) {
      this.actionError = this.docHelpAlerts.find(a => a.severity === 'block')?.message
        || this.i18n.t('sales.selectCustomerError');
      return;
    }

    if (this.docKind === 'Invoice' && !this.editingInvoiceId && !this.docSalesDeliveryNoteIds.length && !this.docSalesDeliveryNoteId) {
      this.actionError = this.i18n.t('sales.invoiceNeedDeliveryNote');
      return;
    }

    this.docLines.forEach(l => this.calcLine(l));
    this.saving = true;
    this.actionError = '';

    if (this.docKind === 'Quote') {
      const quote: Quote = {
        quoteNumber: '',
        customerId: this.docCustomerId,
        date: new Date().toISOString(),
        expirationDate: this.docExpirationDate ? new Date(this.docExpirationDate).toISOString() : new Date().toISOString(),
        status: 'Draft',
        totalHT: this.docTotals.ht,
        totalVat: this.docTotals.vat,
        totalTTC: this.docTotals.ttc,
        notes: this.docNotes || undefined,
        lines: this.docLines.map((l, i) => ({ ...l, lineNumber: i + 1 } as QuoteLine))
      };
      this.businessService.createQuote(quote).subscribe({
        next: (created) => this.onDocCreated(this.i18n.t('sales.quoteCreated', { number: created.quoteNumber }), 0),
        error: (error) => this.onDocError(error, this.i18n.t('sales.action.createQuote'))
      });
      return;
    }

    if (this.docKind === 'Order') {
      const order: SalesOrder = {
        id: this.editingOrderId ?? undefined,
        orderNumber: '',
        customerId: this.docCustomerId,
        date: new Date().toISOString(),
        status: 'Draft',
        totalHT: this.docTotals.ht,
        totalVat: this.docTotals.vat,
        totalTTC: this.docTotals.ttc,
        notes: this.docNotes || undefined,
        lines: this.docLines.map((l, i) => ({
          id: l.id,
          productKey: l.productKey,
          description: l.description,
          quantity: l.quantity,
          deliveredQuantity: l.deliveredQuantity || 0,
          invoicedQuantity: l.invoicedQuantity || 0,
          unitPrice: l.unitPrice,
          vatRate: l.vatRate,
          totalHT: l.totalHT,
          totalTTC: l.totalTTC,
          lineNumber: i + 1
        } as SalesOrderLine))
      };

      if (this.editingOrderId) {
        this.businessService.updateSalesOrder(this.editingOrderId, order).subscribe({
          next: (updated) => this.onDocCreated(this.i18n.t('sales.orderUpdated', { number: updated.orderNumber }), 1),
          error: (error) => this.onDocError(error, this.i18n.t('sales.action.updateOrder'))
        });
        return;
      }

      this.businessService.createSalesOrder(order).subscribe({
        next: (created) => this.onDocCreated(this.i18n.t('sales.orderCreated', { number: created.orderNumber }), 1),
        error: (error) => this.onDocError(error, this.i18n.t('sales.action.createOrder'))
      });
      return;
    }

    const invoice: SalesInvoice = {
      id: this.editingInvoiceId ?? undefined,
      invoiceNumber: '',
      customerId: this.docCustomerId,
      salesOrderId: this.docSalesOrderId ?? undefined,
      salesDeliveryNoteId: this.editingInvoiceId ? undefined : (this.docSalesDeliveryNoteIds[0] ?? this.docSalesDeliveryNoteId ?? undefined),
      salesDeliveryNoteIds: this.editingInvoiceId ? undefined : (this.docSalesDeliveryNoteIds.length ? this.docSalesDeliveryNoteIds : undefined),
      date: new Date().toISOString(),
      dueDate: this.docDueDate ? new Date(this.docDueDate).toISOString() : new Date().toISOString(),
      status: 'Draft',
      totalHT: this.docTotals.ht,
      totalVat: this.docTotals.vat,
      totalTTC: this.docTotals.ttc,
      paidAmount: 0,
      notes: this.docNotes || undefined,
      lines: this.docLines.map((l, i) => ({
        id: l.id,
        productKey: l.productKey,
        description: l.description,
        quantity: l.quantity,
        deliveredQuantity: l.deliveredQuantity || 0,
        orderedQuantity: l.quantity,
        unitPrice: l.unitPrice,
        vatRate: l.vatRate,
        totalHT: l.totalHT,
        totalTTC: l.totalTTC,
        lineNumber: i + 1
      } as SalesInvoiceLine))
    };

    if (this.editingInvoiceId) {
      this.businessService.updateSalesInvoice(this.editingInvoiceId, invoice).subscribe({
        next: (updated) => this.onDocCreated(this.i18n.t('sales.invoiceUpdated', { number: updated.invoiceNumber }), 3),
        error: (error) => this.onDocError(error, this.i18n.t('sales.action.updateInvoice'))
      });
      return;
    }

    this.businessService.createSalesInvoice(invoice).subscribe({
      next: (created) => this.onDocCreated(this.i18n.t('sales.invoiceCreated', { number: created.invoiceNumber }), 3),
      error: (error) => this.onDocError(error, this.i18n.t('sales.action.createInvoice'))
    });
  }

  openCustomerModal(fromDoc = false): void {
    this.openCustomerFromDoc = fromDoc;
    this.editingCustomerId = null;
    this.newCustomer = this.emptyCustomer();
    this.actionError = '';
    this.showCustomerModal = true;
  }

  openEditCustomerModal(customer: Customer): void {
    if (!customer.id) return;
    this.openCustomerFromDoc = false;
    this.editingCustomerId = customer.id;
    this.newCustomer = {
      customerCode: customer.customerCode,
      name: customer.name,
      vatNumber: customer.vatNumber || '',
      address: customer.address || '',
      city: customer.city || '',
      postalCode: customer.postalCode || '',
      country: customer.country || 'BE',
      email: customer.email || '',
      phone: customer.phone || '',
      balance: customer.balance,
      creditLimit: customer.creditLimit ?? 0,
      paymentTerms: customer.paymentTerms || ''
    };
    this.actionError = '';
    this.showCustomerModal = true;
  }

  deleteCustomer(customer: Customer): void {
    if (!customer.id) return;
    if (!confirm(this.i18n.t('sales.confirm.deleteCustomer', { name: customer.name }))) return;
    this.actionError = '';
    this.businessService.deleteCustomer(customer.id).subscribe({
      next: () => {
        this.actionMessage = this.i18n.t('sales.customerDeleted', { name: customer.name });
        this.loadAllData();
      },
      error: (error) => {
        this.actionError = error?.error?.error || error?.error || this.i18n.t('sales.customerDeleteError');
      }
    });
  }

  saveCustomer(): void {
    if (!this.newCustomer.name?.trim()) {
      this.actionError = this.i18n.t('sales.customerNameRequired');
      return;
    }
    this.saving = true;
    this.actionError = '';
    const payload: Customer = {
      customerCode: this.newCustomer.customerCode?.trim() || '',
      name: this.newCustomer.name.trim(),
      vatNumber: this.newCustomer.vatNumber || undefined,
      address: this.newCustomer.address || undefined,
      city: this.newCustomer.city || undefined,
      postalCode: this.newCustomer.postalCode || undefined,
      country: this.newCustomer.country || 'BE',
      email: this.newCustomer.email || undefined,
      phone: this.newCustomer.phone || undefined,
      balance: 0,
      creditLimit: this.newCustomer.creditLimit ?? 0,
      paymentTerms: this.newCustomer.paymentTerms?.trim() || undefined
    };
    const request = this.editingCustomerId
      ? this.businessService.updateCustomer(this.editingCustomerId, {
          ...payload,
          id: this.editingCustomerId,
          balance: this.newCustomer.balance ?? 0,
          creditLimit: this.newCustomer.creditLimit ?? 0
        })
      : this.businessService.createCustomer(payload);

    request.subscribe({
      next: (saved) => {
        this.saving = false;
        this.showCustomerModal = false;
        const wasEdit = !!this.editingCustomerId;
        const fromDoc = this.openCustomerFromDoc;
        const verb = wasEdit ? this.i18n.t('common.updated') : this.i18n.t('common.created');
        this.actionMessage = this.i18n.t('sales.customerSaved', { name: saved.name, code: saved.customerCode, verb });
        this.editingCustomerId = null;
        this.openCustomerFromDoc = false;
        this.refreshMainAfterModal();
        this.businessService.getCustomers().subscribe(c => {
          this.customers = c;
          if (!wasEdit && fromDoc && saved.id) {
            this.docCustomerId = saved.id;
            this.showDocModal = true;
          } else if (!wasEdit) {
            this.selectedTab = 5;
          }
        });
      },
      error: (error) => {
        this.saving = false;
        this.actionError = error?.error?.error || error?.error || this.i18n.t('sales.customerSaveError', {
          action: this.editingCustomerId ? this.i18n.t('sales.customerUpdateAction') : this.i18n.t('sales.customerCreateAction')
        });
      }
    });
  }

  convertQuoteToOrder(quote: Quote): void {
    if (!quote.id) return;
    if ((quote.status || '').toLowerCase() !== 'accepted') {
      this.actionError = this.i18n.t('sales.quoteNeedAccepted');
      return;
    }
    this.actionError = '';
    this.businessService.convertToOrder(quote.id).subscribe({
      next: (order) => {
        this.selectedTab = 1;
        this.actionMessage = this.i18n.t('sales.orderFromQuote', { order: order.orderNumber, quote: quote.quoteNumber });
        this.loadAllData();
      },
      error: (error) => {
        this.actionError = error?.error?.error || error?.error || this.i18n.t('sales.quoteToOrderError');
      }
    });
  }

  acceptQuote(quote: Quote): void {
    if (!quote.id || !this.perm.has(Permissions.QuoteUpdate)) return;
    this.saving = true;
    this.businessService.acceptQuote(quote.id).subscribe({
      next: (updated) => {
        this.saving = false;
        this.actionMessage = this.i18n.t('sales.quoteAccepted', { number: updated.quoteNumber });
        this.businessService.getQuotes().subscribe(q => this.quotes = q);
      },
      error: (error) => {
        this.saving = false;
        this.actionError = error?.error?.error || error?.error || this.i18n.t('sales.genericError');
      }
    });
  }

  canAcceptQuote(quote: Quote): boolean {
    if (!this.perm.has(Permissions.QuoteUpdate) || !quote.id) return false;
    const s = (quote.status || '').toLowerCase();
    return s === 'draft' || s === 'sent' || s === 'pending';
  }

  canConvertQuote(quote: Quote): boolean {
    if (!this.perm.has(Permissions.OrderCreate) || !quote.id) return false;
    return (quote.status || '').toLowerCase() === 'accepted';
  }

  canDeleteQuote(quote: Quote): boolean {
    if (!this.perm.has(Permissions.QuoteDelete) || !quote.id) return false;
    return (quote.status || '').toLowerCase() === 'draft';
  }

  deleteQuote(quote: Quote): void {
    if (!quote.id || !this.canDeleteQuote(quote)) return;
    if (!confirm(this.i18n.t('sales.confirm.deleteQuote', { number: quote.quoteNumber }))) return;
    this.businessService.deleteQuote(quote.id).subscribe({
      next: () => {
        this.actionMessage = this.i18n.t('sales.quoteDeleted', { number: quote.quoteNumber });
        this.quotes = this.quotes.filter(q => q.id !== quote.id);
      },
      error: (err) => {
        this.actionError = typeof err?.error === 'string' ? err.error : (err?.error?.error || this.i18n.t('sales.deleteError'));
      }
    });
  }

  convertOrderToInvoice(order: SalesOrder): void {
    if (!order.id) return;
    this.actionError = '';
    this.businessService.convertToInvoice(order.id).subscribe({
      next: (invoice) => {
        this.selectedTab = 3;
        this.actionMessage = this.i18n.t('sales.invoiceFromOrder', { invoice: invoice.invoiceNumber, order: order.orderNumber });
        this.loadAllData();
      },
      error: (error) => {
        this.actionError = error?.error?.error || error?.error || this.i18n.t('sales.orderToInvoiceError');
      }
    });
  }

  openPaymentModal(invoice: SalesInvoice): void {
    if (!this.canPayInvoice(invoice)) {
      this.actionError = this.payInvoiceHint(invoice) || this.i18n.t('sales.paymentBlocked');
      return;
    }
    this.selectedInvoiceForPayment = invoice;
    this.paymentAmount = +this.invoiceRemaining(invoice).toFixed(2);
    this.paymentMethod = 'Cash';
    this.paymentReference = '';
    this.paymentBank = '';
    this.showPaymentModal = true;
  }

  canPayInvoice(invoice: SalesInvoice): boolean {
    if (!this.perm.has(Permissions.InvoiceUpdate)) return false;
    const status = (invoice.status || '').toLowerCase();
    if (status === 'draft' || status === 'cancelled' || status === 'paid') return false;
    if (this.invoiceRemaining(invoice) <= 0.01) return false;
    return !!invoice.hasDeliveredSource;
  }

  payInvoiceHint(invoice: SalesInvoice): string {
    if (this.invoiceRemaining(invoice) <= 0.01) return '';
    const status = (invoice.status || '').toLowerCase();
    if (status === 'paid') return '';
    if (status === 'draft') return this.i18n.t('sales.paymentNeedValidate');
    if (!invoice.hasDeliveredSource) return this.i18n.t('sales.paymentNeedDelivery');
    return '';
  }

  validateSalesInvoice(invoice: SalesInvoice): void {
    if (!invoice.id || !this.perm.has(Permissions.InvoiceUpdate)) return;
    this.saving = true;
    this.businessService.validateSalesInvoice(invoice.id).subscribe({
      next: (updated) => {
        this.saving = false;
        this.actionMessage = this.i18n.t('sales.invoiceValidated', { number: updated.invoiceNumber });
        this.businessService.getSalesInvoices().subscribe(i => this.invoices = i);
        this.businessService.getCustomers().subscribe(c => this.customers = c);
      },
      error: (err) => {
        this.saving = false;
        this.actionError = typeof err?.error === 'string' ? err.error : (err?.error?.error || this.i18n.t('sales.genericError'));
      }
    });
  }

  invoiceRemaining(invoice: SalesInvoice): number {
    const raw = invoice.remainingAmount != null
      ? Number(invoice.remainingAmount)
      : Number(invoice.totalTTC || 0) - Number(invoice.paidAmount || 0) - Number(invoice.creditedAmount || 0);
    const rounded = Math.round((raw + Number.EPSILON) * 100) / 100;
    return rounded <= 0.01 ? 0 : Math.max(0, rounded);
  }

  /** Aligne la ligne liste sur le détail enrichi (statut Paid, reste dû, etc.). */
  private syncInvoiceListRow(full: SalesInvoice): void {
    if (!full?.id) return;
    const idx = this.invoices.findIndex(i => i.id === full.id);
    if (idx < 0) return;
    this.invoices[idx] = {
      ...this.invoices[idx],
      status: full.status,
      paidAmount: full.paidAmount,
      creditedAmount: full.creditedAmount,
      remainingAmount: full.remainingAmount,
      hasDeliveredSource: full.hasDeliveredSource,
      isOverdue: full.isOverdue
    };
  }

  submitPayment(): void {
    if (!this.selectedInvoiceForPayment?.id) return;
    this.actionError = '';
    this.businessService.recordPayment(
      this.selectedInvoiceForPayment.id,
      this.paymentAmount,
      this.paymentMethod,
      undefined,
      { reference: this.paymentReference || undefined, bank: this.paymentBank || undefined }
    ).subscribe({
      next: () => {
        this.finishModalSuccess(() => { this.showPaymentModal = false; }, this.i18n.t('sales.paymentSaved', { amount: this.paymentAmount.toFixed(2) }));
      },
      error: (error) => {
        this.actionError = error?.error?.error || error?.error || this.i18n.t('sales.paymentError');
      }
    });
  }

  cancelPayment(payment: Payment): void {
    if (!payment.id || !this.perm.has(Permissions.InvoiceUpdate)) return;
    if (!confirm(this.i18n.t('sales.payment.cancelConfirm'))) return;
    this.businessService.cancelPayment(payment.id).subscribe({
      next: () => {
        this.actionMessage = this.i18n.t('sales.payment.cancelled');
        this.loadAllData();
      },
      error: (error) => {
        this.actionError = error?.error?.error || error?.error || this.i18n.t('sales.payment.cancelError');
      }
    });
  }

  paymentInvoiceNumber(payment: Payment): string {
    return payment.salesInvoice?.invoiceNumber || `#${payment.salesInvoiceId}`;
  }

  createCreditNoteFromInvoice(invoice: SalesInvoice): void {
    if (!invoice.id) return;
    this.actionError = '';
    this.creditNoteInvoice = invoice;
    this.creditNoteNotes = '';
    this.creditNoteLines = [];
    this.showCreditNoteModal = true;
    this.creditNoteLoading = true;

    this.businessService.getSalesInvoice(invoice.id).subscribe({
      next: (full) => {
        this.creditNoteInvoice = full;
        this.creditNoteLines = (full.lines || []).map((line, index) => ({
          invoiceLineId: line.id,
          lineNumber: line.lineNumber > 0 ? line.lineNumber : index + 1,
          productKey: line.productKey || '',
          description: line.description || '',
          maxQuantity: line.quantity,
          unitPrice: line.unitPrice,
          vatRate: line.vatRate,
          selected: true,
          quantity: line.quantity
        }));
        this.creditNoteLoading = false;
      },
      error: () => {
        this.creditNoteLoading = false;
        this.showCreditNoteModal = false;
        this.actionError = this.i18n.t('sales.creditNoteCreateError');
      }
    });
  }

  get creditNoteSelectedCount(): number {
    return this.creditNoteLines.filter(l => l.selected && l.quantity > 0).length;
  }

  get creditNotePreviewTotals(): { ht: number; vat: number; ttc: number } {
    let ht = 0;
    let ttc = 0;
    for (const line of this.creditNoteLines) {
      if (!line.selected || line.quantity <= 0) continue;
      const qty = Math.min(line.quantity, line.maxQuantity);
      const lineHt = qty * line.unitPrice;
      ht += lineHt;
      ttc += lineHt * (1 + (line.vatRate / 100));
    }
    return { ht, vat: ttc - ht, ttc };
  }

  toggleCreditNoteSelectAll(select: boolean): void {
    for (const line of this.creditNoteLines) {
      line.selected = select;
      if (select && (!line.quantity || line.quantity <= 0)) {
        line.quantity = line.maxQuantity;
      }
    }
  }

  onCreditNoteLineToggle(line: { selected: boolean; quantity: number; maxQuantity: number }): void {
    if (line.selected && (!line.quantity || line.quantity <= 0)) {
      line.quantity = line.maxQuantity;
    }
  }

  clampCreditNoteQty(line: { quantity: number; maxQuantity: number; selected: boolean }): void {
    if (line.quantity > line.maxQuantity) line.quantity = line.maxQuantity;
    if (line.quantity < 0) line.quantity = 0;
    if (line.quantity > 0) line.selected = true;
  }

  submitCreditNoteFromInvoice(): void {
    if (!this.creditNoteInvoice?.id) return;
    const lines = this.creditNoteLines
      .filter(l => l.selected && l.quantity > 0)
      .map(l => ({
        invoiceLineId: l.invoiceLineId,
        lineNumber: l.lineNumber,
        productKey: l.productKey,
        quantity: Math.min(l.quantity, l.maxQuantity)
      }));

    if (lines.length === 0) {
      this.actionError = this.i18n.t('sales.creditNoteSelectLines');
      return;
    }

    this.saving = true;
    this.actionError = '';
    this.businessService.createCreditNoteFromInvoice(
      this.creditNoteInvoice.id,
      this.creditNoteNotes || undefined,
      lines
    ).subscribe({
      next: (creditNote) => {
        this.saving = false;
        this.selectedTab = 4;
        this.finishModalSuccess(
          () => { this.showCreditNoteModal = false; },
          this.i18n.t('sales.creditNoteFromInvoice', {
            creditNote: creditNote.creditNoteNumber,
            invoice: this.creditNoteInvoice?.invoiceNumber || ''
          })
        );
      },
      error: (error) => {
        this.saving = false;
        this.actionError = error?.error?.error || error?.error || this.i18n.t('sales.creditNoteCreateError');
      }
    });
  }

  validateCreditNote(creditNote: CreditNote): void {
    if (!creditNote.id) return;
    this.actionError = '';
    this.businessService.validateCreditNote(creditNote.id).subscribe({
      next: (updated) => {
        this.actionMessage = this.i18n.t('sales.creditNoteValidatedSettled', { number: updated.creditNoteNumber });
        this.loadAllData();
      },
      error: (error) => {
        this.actionError = error?.error?.error || error?.error || this.i18n.t('sales.creditNoteValidateError');
      }
    });
  }

  applyCreditNote(creditNote: CreditNote): void {
    if (!creditNote.id) return;
    this.actionError = '';
    this.businessService.applyCreditNote(creditNote.id).subscribe({
      next: (updated) => {
        this.actionMessage = this.i18n.t('sales.creditNoteApplied', { number: updated.creditNoteNumber });
        this.loadAllData();
      },
      error: (error) => {
        this.actionError = error?.error?.error || error?.error || this.i18n.t('sales.creditNoteApplyError');
      }
    });
  }

  customerName(creditNote: CreditNote): string {
    return creditNote.customer?.name || this.i18n.t('sales.customerHash', { id: creditNote.customerId });
  }

  isExpanded(kind: 'Quote' | 'Order' | 'Invoice' | 'CreditNote' | 'DeliveryNote', id?: number): boolean {
    return !!id && this.expandedRowKey === this.rowKey(kind, id);
  }

  toggleQuoteDetail(quote: Quote): void {
    this.toggleDetail('Quote', quote.id, () => this.businessService.getQuote(quote.id!));
  }

  toggleOrderDetail(order: SalesOrder): void {
    this.toggleDetail('Order', order.id, () => this.businessService.getSalesOrder(order.id!));
  }

  toggleInvoiceDetail(invoice: SalesInvoice): void {
    this.toggleDetail('Invoice', invoice.id, () => this.businessService.getSalesInvoice(invoice.id!));
  }

  toggleCreditNoteDetail(creditNote: CreditNote): void {
    this.toggleDetail('CreditNote', creditNote.id, () => this.businessService.getCreditNote(creditNote.id!));
  }

  toggleDeliveryNoteDetail(note: SalesDeliveryNote): void {
    this.toggleDetail('DeliveryNote', note.id, () => this.businessService.getSalesDeliveryNote(note.id!));
  }

  private rowKey(kind: string, id: number): string {
    return `${kind}:${id}`;
  }

  private toggleDetail<T extends Quote | SalesOrder | SalesInvoice | CreditNote | SalesDeliveryNote>(
    kind: 'Quote' | 'Order' | 'Invoice' | 'CreditNote' | 'DeliveryNote',
    id: number | undefined,
    loader: () => Observable<T>
  ): void {
    if (!id) return;
    const key = this.rowKey(kind, id);
    if (this.expandedRowKey === key) {
      this.expandedRowKey = null;
      this.resetDetail();
      return;
    }

    this.expandedRowKey = key;
    this.resetDetail();
    this.detailKind = kind;
    this.detailLoading = true;
    loader().subscribe({
      next: (full) => {
        if (kind === 'Quote') this.detailQuote = full as Quote;
        else if (kind === 'Order') {
          this.detailOrder = full as SalesOrder;
          this.loadDocumentAudit('Order', id);
        }
        else if (kind === 'Invoice') {
          this.detailInvoice = full as SalesInvoice;
          this.syncInvoiceListRow(full as SalesInvoice);
          this.loadDocumentAudit('Invoice', id);
        }
        else if (kind === 'CreditNote') this.detailCreditNote = full as CreditNote;
        else this.detailDeliveryNote = full as SalesDeliveryNote;
        this.detailLoading = false;
      },
      error: (error) => {
        this.detailLoading = false;
        this.expandedRowKey = null;
        this.actionError = error?.error?.error || error?.error || this.i18n.t('sales.detailLoadError');
      }
    });
  }

  get detailTitle(): string {
    switch (this.detailKind) {
      case 'Quote': return this.i18n.t('sales.detailTitle.quote', { number: this.detailQuote?.quoteNumber || '' });
      case 'Order': return this.i18n.t('sales.detailTitle.order', { number: this.detailOrder?.orderNumber || '' });
      case 'Invoice': return this.i18n.t('sales.detailTitle.invoice', { number: this.detailInvoice?.invoiceNumber || '' });
      case 'CreditNote': return this.i18n.t('sales.detailTitle.creditNote', { number: this.detailCreditNote?.creditNoteNumber || '' });
      case 'DeliveryNote': return this.i18n.t('sales.detailTitle.deliveryNote', { number: this.detailDeliveryNote?.deliveryNumber || '' });
      default: return this.i18n.t('common.detail');
    }
  }

  get detailPartyName(): string {
    if (this.detailQuote) return this.detailQuote.customer?.name || this.i18n.t('sales.customerHash', { id: this.detailQuote.customerId });
    if (this.detailOrder) return this.detailOrder.customer?.name || this.i18n.t('sales.customerHash', { id: this.detailOrder.customerId });
    if (this.detailInvoice) return this.detailInvoice.customer?.name || this.i18n.t('sales.customerHash', { id: this.detailInvoice.customerId });
    if (this.detailCreditNote) return this.customerName(this.detailCreditNote);
    if (this.detailDeliveryNote) return this.detailDeliveryNote.customer?.name || this.i18n.t('sales.customerHash', { id: this.detailDeliveryNote.customerId });
    return '-';
  }

  get detailLines(): Array<{
    productKey: string;
    description: string;
    quantity: number;
    unitPrice: number;
    vatRate: number;
    totalHT: number;
    totalTTC: number;
    deliveredQuantity?: number;
    remainingQuantity?: number;
    extra?: string;
  }> {
    if (this.detailQuote?.lines) {
      return this.detailQuote.lines.map(l => ({ ...l }));
    }
    if (this.detailOrder?.lines) {
      return this.detailOrder.lines.map(l => ({
        ...l,
        deliveredQuantity: Number(l.deliveredQuantity || 0),
        remainingQuantity: this.remainingQty(l),
        extra: this.i18n.t('sales.label.delivered', { qty: l.deliveredQuantity || 0 })
      }));
    }
    if (this.detailInvoice?.lines) {
      return this.detailInvoice.lines.map(l => ({ ...l }));
    }
    if (this.detailCreditNote?.lines) {
      return this.detailCreditNote.lines.map(l => ({ ...l }));
    }
    if (this.detailDeliveryNote?.lines) {
      return this.detailDeliveryNote.lines.map(l => ({
        productKey: l.productKey,
        description: l.description,
        quantity: l.deliveredQuantity,
        unitPrice: l.unitPrice,
        vatRate: l.vatRate,
        totalHT: l.totalHT,
        totalTTC: l.totalTTC,
        extra: this.i18n.t('sales.label.ordered', { qty: l.orderedQuantity })
      }));
    }
    return [];
  }

  get detailTotals(): { ht: number; vat: number; ttc: number; paid?: number; credited?: number; remaining?: number; status?: string; date?: string; notes?: string } {
    const doc = this.detailQuote || this.detailOrder || this.detailInvoice || this.detailCreditNote;
    if (doc) return {
      ht: doc.totalHT,
      vat: doc.totalVat,
      ttc: doc.totalTTC,
      paid: this.detailInvoice?.paidAmount,
      credited: this.detailInvoice?.creditedAmount || 0,
      remaining: this.detailInvoice ? this.invoiceRemaining(this.detailInvoice) : undefined,
      status: doc.status,
      date: doc.date,
      notes: doc.notes
    };
    if (this.detailDeliveryNote) return {
      ht: this.detailDeliveryNote.totalHT,
      vat: this.detailDeliveryNote.totalVat,
      ttc: this.detailDeliveryNote.totalTTC,
      status: this.detailDeliveryNote.status,
      date: this.detailDeliveryNote.deliveryDate,
      notes: this.detailDeliveryNote.notes
    };
    return { ht: 0, vat: 0, ttc: 0 };
  }

  private resetDetail(): void {
    this.detailQuote = null;
    this.detailOrder = null;
    this.detailInvoice = null;
    this.detailCreditNote = null;
    this.detailDeliveryNote = null;
    this.detailAudit = [];
    this.detailAuditLoading = false;
    this.actionError = '';
  }

  private loadDocumentAudit(kind: 'Order' | 'Invoice', documentId: number): void {
    this.detailAuditLoading = true;
    this.detailAudit = [];
    const request = kind === 'Invoice'
      ? this.businessService.getSalesInvoiceAudit(documentId)
      : this.businessService.getSalesOrderAudit(documentId);
    request.subscribe({
      next: (logs) => {
        this.detailAudit = logs || [];
        this.detailAuditLoading = false;
      },
      error: () => {
        this.detailAudit = [];
        this.detailAuditLoading = false;
      }
    });
  }

  canRestoreTrashItem(item: SalesTrashItem): boolean {
    if (!item.canRestore) return false;
    switch ((item.documentType || '').toLowerCase()) {
      case 'invoice': return this.perm.has(Permissions.InvoiceUpdate);
      case 'order': return this.perm.has(Permissions.OrderUpdate);
      case 'deliverynote': return this.perm.has(Permissions.DeliveryNoteCreate);
      case 'quote': return this.perm.has(Permissions.QuoteUpdate);
      default: return false;
    }
  }

  canPurgeTrashItem(item: SalesTrashItem): boolean {
    if (item.canPurge === false) return false;
    switch ((item.documentType || '').toLowerCase()) {
      case 'invoice': return this.perm.has(Permissions.InvoiceDelete);
      case 'order': return this.perm.has(Permissions.OrderDelete);
      case 'deliverynote': return this.perm.has(Permissions.DeliveryNoteDelete);
      case 'quote': return this.perm.has(Permissions.QuoteDelete);
      default: return false;
    }
  }

  canEmptyTrash(): boolean {
    return this.trashItems.length > 0 && (
      this.perm.has(Permissions.InvoiceDelete)
      || this.perm.has(Permissions.OrderDelete)
      || this.perm.has(Permissions.DeliveryNoteDelete)
      || this.perm.has(Permissions.QuoteDelete)
    );
  }

  trashTypeLabel(item: SalesTrashItem): string {
    switch ((item.documentType || '').toLowerCase()) {
      case 'invoice': return this.i18n.t('sales.trash.type.invoice');
      case 'order': return this.i18n.t('sales.trash.type.order');
      case 'deliverynote': return this.i18n.t('sales.trash.type.deliveryNote');
      case 'quote': return this.i18n.t('sales.trash.type.quote');
      default: return item.documentType;
    }
  }

  restoreTrashItem(item: SalesTrashItem): void {
    if (!item?.id || !this.canRestoreTrashItem(item)) return;
    const key = `${item.documentType}:${item.id}`;
    this.trashRestoringKey = key;
    this.actionError = '';
    this.businessService.restoreSalesTrashItem(item.documentType, item.id).subscribe({
      next: () => {
        this.trashRestoringKey = null;
        this.actionMessage = this.i18n.t('sales.trash.restored', { number: item.number });
        this.loadTrash();
        this.loadAllData();
      },
      error: (err) => {
        this.trashRestoringKey = null;
        this.actionError = err?.error?.error || err?.error || this.i18n.t('sales.trash.restoreError');
        this.loadTrash();
      }
    });
  }

  purgeTrashItem(item: SalesTrashItem): void {
    if (!item?.id || !this.canPurgeTrashItem(item)) return;
    if (!confirm(this.i18n.t('sales.trash.purgeConfirm', { number: item.number }))) return;
    this.trashBusy = true;
    this.actionError = '';
    this.businessService.purgeSalesTrashItem(item.documentType, item.id).subscribe({
      next: () => {
        this.trashBusy = false;
        this.actionMessage = this.i18n.t('sales.trash.purged', { number: item.number });
        this.loadTrash();
      },
      error: (err) => {
        this.trashBusy = false;
        this.actionError = err?.error?.error || err?.error || this.i18n.t('sales.trash.purgeError');
      }
    });
  }

  emptyTrash(): void {
    if (!this.canEmptyTrash()) return;
    if (!confirm(this.i18n.t('sales.trash.emptyConfirm'))) return;
    this.trashBusy = true;
    this.actionError = '';
    this.businessService.emptySalesTrash().subscribe({
      next: (res) => {
        this.trashBusy = false;
        this.actionMessage = this.i18n.t('sales.trash.emptied', { count: res?.purged ?? 0 });
        this.loadTrash();
      },
      error: (err) => {
        this.trashBusy = false;
        this.actionError = err?.error?.error || err?.error || this.i18n.t('sales.trash.emptyError');
      }
    });
  }

  private onDocCreated(message: string, tab: number): void {
    this.saving = false;
    this.showDocModal = false;
    const editedOrderId = this.editingOrderId;
    const editedInvoiceId = this.editingInvoiceId;
    this.editingOrderId = null;
    this.editingInvoiceId = null;
    this.editingOrderCommitted = false;
    this.selectedTab = tab;
    this.actionMessage = message;

    const reopenOrderId =
      this.detailKind === 'Order' && this.detailOrder?.id != null
      && (editedOrderId == null || this.detailOrder.id === editedOrderId)
        ? this.detailOrder.id!
        : null;
    const reopenInvoiceId =
      this.detailKind === 'Invoice' && this.detailInvoice?.id != null
      && (editedInvoiceId == null || this.detailInvoice.id === editedInvoiceId)
        ? this.detailInvoice.id!
        : null;

    this.expandedRowKey = null;
    this.resetDetail();
    this.loadAllData();

    if (reopenOrderId != null) {
      this.detailLoading = true;
      this.detailKind = 'Order';
      this.expandedRowKey = this.rowKey('Order', reopenOrderId);
      this.businessService.getSalesOrder(reopenOrderId).subscribe({
        next: (full) => {
          this.detailOrder = full;
          this.detailLoading = false;
          this.loadDocumentAudit('Order', reopenOrderId);
        },
        error: () => {
          this.expandedRowKey = null;
          this.resetDetail();
          this.detailLoading = false;
        }
      });
      return;
    }

    if (reopenInvoiceId != null) {
      this.detailLoading = true;
      this.detailKind = 'Invoice';
      this.expandedRowKey = this.rowKey('Invoice', reopenInvoiceId);
      this.businessService.getSalesInvoice(reopenInvoiceId).subscribe({
        next: (full) => {
          this.detailInvoice = full;
          this.detailLoading = false;
          this.loadDocumentAudit('Invoice', reopenInvoiceId);
        },
        error: () => {
          this.expandedRowKey = null;
          this.resetDetail();
          this.detailLoading = false;
        }
      });
    }
  }

  private onDocError(error: any, action: string): void {
    this.saving = false;
    this.actionError = error?.error?.error || error?.error || this.i18n.t('sales.errorDuring', { action });
  }

  private emptyLine(lineNumber: number): DocLineDraft {
    return {
      productKey: '',
      description: '',
      quantity: 1,
      unitPrice: 0,
      vatRate: 21,
      totalHT: 0,
      totalTTC: 0,
      lineNumber
    };
  }

  // ── Delivery Notes ───────────────────────────────────────────────────────────

  openDeliveryNoteModal(fromOrder?: SalesOrder): void {
    if (!this.perm.has(Permissions.DeliveryNoteCreate)) return;
    this.showDeliveryNoteModal = true;
    this.dnDate = new Date().toISOString().slice(0, 10);
    this.dnNotes = '';
    this.dnLines = [];
    if (fromOrder) {
      this.dnCustomerId = fromOrder.customerId;
      this.dnSalesOrderId = fromOrder.id ?? null;
      this.dnLines = fromOrder.lines.map((l, i) => ({
        productKey: l.productKey,
        description: l.description,
        orderedQuantity: l.quantity,
        deliveredQuantity: l.quantity,
        unitPrice: l.unitPrice,
        vatRate: l.vatRate,
        totalHT: l.totalHT,
        totalTTC: l.totalTTC,
        lineNumber: i + 1
      }));
    } else {
      this.dnCustomerId = null;
      this.dnSalesOrderId = null;
      this.addDnLine();
    }
  }

  onDnOrderChange(orderId: number | null): void {
    this.dnSalesOrderId = orderId;
    if (!orderId) {
      this.dnLines = [];
      this.addDnLine();
      return;
    }
    const order = this.orders.find(o => o.id === Number(orderId));
    if (!order) return;
    this.dnCustomerId = order.customerId;
    this.dnLines = order.lines.map((l, i) => ({
      productKey: l.productKey,
      description: l.description,
      orderedQuantity: l.quantity,
      deliveredQuantity: l.quantity,
      unitPrice: l.unitPrice,
      vatRate: l.vatRate,
      totalHT: l.totalHT,
      totalTTC: l.totalTTC,
      lineNumber: i + 1
    }));
  }

  addDnLine(): void {
    this.dnLines.push({ productKey: '', description: '', orderedQuantity: 0, deliveredQuantity: 0, unitPrice: 0, vatRate: 21, totalHT: 0, totalTTC: 0, lineNumber: this.dnLines.length + 1 });
  }

  removeDnLine(i: number): void {
    this.dnLines.splice(i, 1);
    this.dnLines.forEach((l, idx) => l.lineNumber = idx + 1);
  }

  calcDnLine(line: { deliveredQuantity: number; unitPrice: number; vatRate: number; totalHT: number; totalTTC: number }): void {
    line.totalHT = line.deliveredQuantity * line.unitPrice;
    line.totalTTC = line.totalHT * (1 + line.vatRate / 100);
  }

  get dnTotals(): { ht: number; vat: number; ttc: number } {
    const ht = this.dnLines.reduce((s, l) => s + l.totalHT, 0);
    const vat = this.dnLines.reduce((s, l) => s + l.totalHT * (l.vatRate / 100), 0);
    return { ht, vat, ttc: ht + vat };
  }

  saveDeliveryNote(): void {
    if (!this.perm.has(Permissions.DeliveryNoteCreate) || !this.dnCustomerId) return;
    if (!this.dnSalesOrderId) {
      this.actionError = this.i18n.t('sales.deliveryNoteNeedOrder');
      return;
    }
    const note: SalesDeliveryNote = {
      deliveryNumber: '',
      customerId: this.dnCustomerId,
      salesOrderId: this.dnSalesOrderId,
      deliveryDate: this.dnDate || new Date().toISOString(),
      status: 'Draft',
      totalHT: this.dnTotals.ht,
      totalVat: this.dnTotals.vat,
      totalTTC: this.dnTotals.ttc,
      notes: this.dnNotes,
      lines: this.dnLines.map(l => ({ ...l }))
    };
    this.saving = true;
    this.businessService.createSalesDeliveryNote(note).subscribe({
      next: (created) => {
        this.saving = false;
        this.finishModalSuccess(
          () => { this.showDeliveryNoteModal = false; },
          this.i18n.t('sales.deliveryNoteCreated', { number: created.deliveryNumber })
        );
      },
      error: (err) => {
        this.saving = false;
        this.actionError = err?.error?.error || err?.error || this.i18n.t('sales.deliveryNoteCreateError');
      }
    });
  }

  canCreateDeliveryNoteFromOrder(order: SalesOrder): boolean {
    if (!this.perm.has(Permissions.DeliveryNoteCreate)) return false;
    const s = (order.status || '').toLowerCase();
    if (s === 'cancelled' || s === 'draft' || s === 'pending') return false;
    // Un BL Draft/Sent réserve déjà les quantités : pas de second BL tant qu'il n'est pas validé/annulé
    const hasOpenDn = this.deliveryNotes.some(dn =>
      dn.salesOrderId === order.id
      && ['draft', 'sent'].includes((dn.status || '').toLowerCase()));
    if (hasOpenDn) return false;
    return this.orderHasRemainingToDeliver(order);
  }

  canHoldOrder(order: SalesOrder): boolean {
    if (!this.perm.has(Permissions.OrderUpdate) || !order.id) return false;
    const s = (order.status || '').toLowerCase();
    if (s !== 'draft' && s !== 'confirmed') return false;
    const lines = order.lines || [];
    if (lines.some(l => Number(l.deliveredQuantity || 0) > 0 || Number(l.invoicedQuantity || 0) > 0)) return false;
    return true;
  }

  canApproveOrder(order: SalesOrder): boolean {
    return !!order.id
      && this.perm.has(Permissions.OrderUpdate)
      && (order.status || '').toLowerCase() === 'pending';
  }

  canArchiveOrder(order: SalesOrder): boolean {
    if (!this.perm.has(Permissions.OrderUpdate) || !order.id) return false;
    const s = (order.status || '').toLowerCase();
    return ['closed', 'cancelled', 'invoiced', 'delivered'].includes(s);
  }

  archiveSalesOrder(order: SalesOrder): void {
    if (!order.id || !this.canArchiveOrder(order)) return;
    this.saving = true;
    this.businessService.archiveSalesOrder(order.id).subscribe({
      next: (updated) => {
        this.saving = false;
        this.actionMessage = this.i18n.t('sales.orderArchived', { number: updated.orderNumber });
        this.businessService.getSalesOrders().subscribe(o => this.orders = o);
        this.loadPilotage();
      },
      error: (err) => {
        this.saving = false;
        this.actionError = typeof err?.error === 'string' ? err.error : (err?.error?.error || this.i18n.t('sales.genericError'));
      }
    });
  }

  createDeliveryNoteFromOrder(order: SalesOrder): void {
    if (!this.perm.has(Permissions.DeliveryNoteCreate) || !this.canCreateDeliveryNoteFromOrder(order)) return;
    this.saving = true;
    this.businessService.createSalesDeliveryNoteFromOrder(order.id!).subscribe({
      next: (note) => {
        this.saving = false;
        const backorder = (note.notes || '').includes('Reliquat');
        this.actionMessage = backorder
          ? this.i18n.t('sales.deliveryNoteFromOrderPartial', { delivery: note.deliveryNumber, order: order.orderNumber })
          : this.i18n.t('sales.deliveryNoteFromOrder', { delivery: note.deliveryNumber, order: order.orderNumber });
        this.businessService.getSalesDeliveryNotes().subscribe(d => this.deliveryNotes = d);
        this.businessService.getSalesOrders().subscribe(o => this.orders = o);
        this.loadPilotage();
      },
      error: (err) => {
        this.saving = false;
        const msg = typeof err?.error === 'string' ? err.error : (err?.error?.error || err?.error);
        this.actionError = msg || this.i18n.t('sales.genericError');
      }
    });
  }

  convertDeliveryNoteToInvoice(note: SalesDeliveryNote): void {
    if (!note.id) return;
    this.saving = true;
    this.businessService.convertDeliveryNoteToInvoice(note.id).subscribe({
      next: (inv) => {
        this.saving = false;
        this.actionMessage = this.i18n.t('sales.invoiceFromDeliveryNote', { invoice: inv.invoiceNumber, delivery: note.deliveryNumber });
        this.businessService.getSalesDeliveryNotes().subscribe(d => this.deliveryNotes = d);
        this.businessService.getSalesInvoices().subscribe(i => this.invoices = i);
      },
      error: (err) => {
        this.saving = false;
        this.actionError = err?.error?.error || err?.error || this.i18n.t('sales.genericError');
      }
    });
  }

  validateDeliveryNote(note: SalesDeliveryNote): void {
    if (!note.id || !this.perm.has(Permissions.DeliveryNoteCreate)) return;
    this.saving = true;
    this.businessService.validateSalesDeliveryNote(note.id).subscribe({
      next: (updated) => {
        this.saving = false;
        this.actionMessage = this.i18n.t('sales.deliveryNoteValidated', { number: updated.deliveryNumber });
        this.businessService.getSalesDeliveryNotes().subscribe(d => this.deliveryNotes = d);
        this.businessService.getSalesOrders().subscribe(o => this.orders = o);
      },
      error: (err) => {
        this.saving = false;
        this.actionError = typeof err?.error === 'string' ? err.error : (err?.error?.error || this.i18n.t('sales.genericError'));
      }
    });
  }

  confirmSalesOrder(order: SalesOrder): void {
    if (!order.id || !this.perm.has(Permissions.OrderUpdate)) return;
    this.saving = true;
    this.businessService.confirmSalesOrder(order.id).subscribe({
      next: (updated) => {
        this.saving = false;
        const pending = (updated.status || '').toLowerCase() === 'pending';
        this.actionMessage = pending
          ? this.i18n.t('sales.orderPendingCredit', { number: updated.orderNumber })
          : this.i18n.t('sales.orderConfirmed', { number: updated.orderNumber });
        this.businessService.getSalesOrders().subscribe(o => this.orders = o);
        this.loadPilotage();
      },
      error: (err) => {
        this.saving = false;
        this.actionError = typeof err?.error === 'string' ? err.error : (err?.error?.error || this.i18n.t('sales.genericError'));
      }
    });
  }

  approveSalesOrder(order: SalesOrder): void {
    if (!order.id || !this.canApproveOrder(order)) return;
    this.saving = true;
    this.businessService.approveSalesOrder(order.id).subscribe({
      next: (updated) => {
        this.saving = false;
        this.actionMessage = this.i18n.t('sales.orderApproved', { number: updated.orderNumber });
        this.businessService.getSalesOrders().subscribe(o => this.orders = o);
        this.loadPilotage();
      },
      error: (err) => {
        this.saving = false;
        this.actionError = typeof err?.error === 'string' ? err.error : (err?.error?.error || this.i18n.t('sales.genericError'));
      }
    });
  }

  holdSalesOrder(order: SalesOrder): void {
    if (!order.id || !this.canHoldOrder(order)) return;
    const reason = prompt(this.i18n.t('sales.holdReasonPrompt')) || '';
    this.saving = true;
    this.businessService.holdSalesOrder(order.id, reason.trim() || undefined).subscribe({
      next: (updated) => {
        this.saving = false;
        this.actionMessage = this.i18n.t('sales.orderHeld', { number: updated.orderNumber });
        this.businessService.getSalesOrders().subscribe(o => this.orders = o);
        this.loadPilotage();
      },
      error: (err) => {
        this.saving = false;
        this.actionError = typeof err?.error === 'string' ? err.error : (err?.error?.error || this.i18n.t('sales.genericError'));
      }
    });
  }

  openOrderFromPilotage(orderId: number): void {
    this.selectedTab = 1;
    this.expandedRowKey = null;
    this.resetDetail();
    this.detailLoading = true;
    this.detailKind = 'Order';
    this.expandedRowKey = this.rowKey('Order', orderId);
    this.businessService.getSalesOrder(orderId).subscribe({
      next: (full) => {
        if (!this.orders.some(o => o.id === full.id)) this.orders = [full, ...this.orders];
        else this.orders = this.orders.map(o => o.id === full.id ? full : o);
        this.detailOrder = full;
        this.detailLoading = false;
      },
      error: () => {
        this.expandedRowKey = null;
        this.resetDetail();
        this.detailLoading = false;
      }
    });
  }

  downloadDeliveryNotePdf(note: SalesDeliveryNote): void {
    if (!note.id) return;
    this.pdfDownloading = true;
    this.businessService.downloadSalesDeliveryNotePdf(note.id).subscribe({
      next: (blob) => {
        this.pdfDownloading = false;
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url; a.download = `${note.deliveryNumber}.pdf`; a.click();
        window.URL.revokeObjectURL(url);
      },
      error: () => { this.pdfDownloading = false; this.actionError = this.i18n.t('sales.pdfGenericError'); }
    });
  }

  deleteDeliveryNote(note: SalesDeliveryNote): void {
    if (!note.id) return;
    if (!confirm(this.i18n.t('sales.confirm.deleteDeliveryNote', { number: note.deliveryNumber }))) return;
    this.businessService.deleteSalesDeliveryNote(note.id).subscribe({
      next: () => {
        this.actionMessage = this.i18n.t('sales.deliveryNoteDeleted', { number: note.deliveryNumber });
        this.deliveryNotes = this.deliveryNotes.filter(n => n.id !== note.id);
      },
      error: (err) => { this.actionError = err?.error?.error || this.i18n.t('sales.deleteError'); }
    });
  }

  canCancelDeliveryNote(note: SalesDeliveryNote): boolean {
    if (!this.perm.has(Permissions.DeliveryNoteCreate) || !note.id) return false;
    const s = (note.status || '').toLowerCase();
    if (s === 'cancelled' || s === 'invoiced') return false;
    if (note.salesInvoiceId) return false;
    return s === 'draft' || s === 'sent' || s === 'delivered';
  }

  cancelDeliveryNote(note: SalesDeliveryNote): void {
    if (!note.id || !this.canCancelDeliveryNote(note)) return;
    const reason = prompt(this.i18n.t('sales.cancelReasonPrompt'));
    if (reason == null) return;
    if (!reason.trim()) {
      this.actionError = this.i18n.t('sales.cancelReasonRequired');
      return;
    }
    this.saving = true;
    this.businessService.cancelSalesDeliveryNote(note.id, reason.trim()).subscribe({
      next: (updated) => {
        this.saving = false;
        this.actionMessage = this.i18n.t('sales.deliveryNoteCancelled', { number: updated.deliveryNumber });
        this.businessService.getSalesDeliveryNotes().subscribe(d => this.deliveryNotes = d);
        this.businessService.getSalesOrders().subscribe(o => this.orders = o);
      },
      error: (err) => {
        this.saving = false;
        const msg = typeof err?.error === 'string' ? err.error : (err?.error?.error || err?.error);
        this.actionError = msg || this.i18n.t('sales.genericError');
      }
    });
  }

  private emptyCustomer(): Partial<Customer> {
    return {
      customerCode: '',
      name: '',
      vatNumber: '',
      address: '',
      city: '',
      postalCode: '',
      country: 'BE',
      email: '',
      phone: '',
      balance: 0,
      creditLimit: 0,
      paymentTerms: ''
    };
  }

  private toDateInput(date: Date): string {
    return date.toISOString().slice(0, 10);
  }

  // ── Duplicate quote ──────────────────────────────────────────────────────────

  duplicateQuote(quote: Quote): void {
    if (!quote.id || !this.perm.has(Permissions.QuoteCreate)) return;
    this.actionError = '';
    this.businessService.duplicateQuote(quote.id).subscribe({
      next: (created) => {
        this.actionMessage = this.i18n.t('sales.quoteDuplicated', { source: quote.quoteNumber, number: created.quoteNumber });
        this.quotes = [created, ...this.quotes];
      },
      error: (err) => {
        this.actionError = err?.error?.error || err?.error || this.i18n.t('sales.returns.error');
      }
    });
  }

  // ── Retours clients (BRC) ────────────────────────────────────────────────────

  get deliverableNotesForReturn(): SalesDeliveryNote[] {
    const q = (this.returnDnSearch || '').trim().toLowerCase();
    return this.deliveryNotes
      .filter(dn => ['delivered', 'invoiced'].includes((dn.status || '').toLowerCase()))
      .filter(dn => this.deliveryNoteHasReturnableQty(dn))
      .filter(dn => {
        if (!q) return true;
        const num = (dn.deliveryNumber || '').toLowerCase();
        const cust = (dn.customer?.name || '').toLowerCase();
        return num.includes(q) || cust.includes(q) || String(dn.customerId).includes(q);
      })
      .slice()
      .sort((a, b) => (b.deliveryDate || '').localeCompare(a.deliveryDate || '')
        || (b.deliveryNumber || '').localeCompare(a.deliveryNumber || ''));
  }

  /** Reliquat retournable > 0 (qté livrée − BRC non annulés). */
  private deliveryNoteHasReturnableQty(dn: SalesDeliveryNote): boolean {
    if (!dn.id) return false;
    const lines = dn.lines || [];
    if (!lines.length) return true; // liste légère sans lignes : laisser choisir, l'API tranchera
    const returned = new Map<string, number>();
    for (const r of this.salesReturns) {
      if (r.salesDeliveryNoteId !== dn.id) continue;
      if ((r.status || '').toLowerCase() === 'cancelled') continue;
      for (const l of r.lines || []) {
        const key = (l.productKey || '').trim().toLowerCase();
        returned.set(key, (returned.get(key) || 0) + Number(l.quantity || 0));
      }
    }
    return lines.some(l => {
      const key = (l.productKey || '').trim().toLowerCase();
      const delivered = Number(l.deliveredQuantity || 0);
      return delivered - (returned.get(key) || 0) > 0.0001;
    });
  }

  openReturnModal(): void {
    if (!this.perm.has(Permissions.SalesReturnCreate)) return;
    this.returnDeliveryNoteId = null;
    this.returnNotes = '';
    this.returnDnSearch = '';
    this.actionError = '';
    this.showReturnModal = true;
    this.returnDnLoading = true;
    // Recharger BL + BRC pour une liste à jour (beaucoup de BL / reliquats).
    this.businessService.getSalesDeliveryNotes().subscribe({
      next: (d) => {
        this.deliveryNotes = d || [];
        this.loadReturns();
        this.returnDnLoading = false;
      },
      error: () => {
        this.returnDnLoading = false;
      }
    });
  }

  submitReturnFromDelivery(): void {
    if (!this.returnDeliveryNoteId) {
      this.actionError = this.i18n.t('sales.returns.selectDeliveryNoteError');
      return;
    }
    this.saving = true;
    this.actionError = '';
    this.businessService.createSalesReturnFromDelivery({
      salesDeliveryNoteId: this.returnDeliveryNoteId,
      notes: this.returnNotes || undefined
    }).subscribe({
      next: (created) => {
        this.saving = false;
        this.finishModalSuccess(
          () => { this.showReturnModal = false; },
          this.i18n.t('sales.returns.created', { number: created.returnNumber })
        );
      },
      error: (err) => {
        this.saving = false;
        this.actionError = err?.error?.error || err?.error || this.i18n.t('sales.returns.error');
      }
    });
  }

  canReceiveReturn(r: SalesReturn): boolean {
    return !!r.id && this.perm.has(Permissions.SalesReturnUpdate) && (r.status || '').toLowerCase() === 'draft';
  }

  canControlReturn(r: SalesReturn): boolean {
    return !!r.id && this.perm.has(Permissions.SalesReturnUpdate) && (r.status || '').toLowerCase() === 'received';
  }

  canIntegrateReturn(r: SalesReturn): boolean {
    if (!r.id || !this.perm.has(Permissions.SalesReturnUpdate)) return false;
    const s = (r.status || '').toLowerCase();
    return s === 'received' || s === 'controlled';
  }

  canCancelReturn(r: SalesReturn): boolean {
    if (!r.id || !this.perm.has(Permissions.SalesReturnUpdate)) return false;
    const s = (r.status || '').toLowerCase();
    return s !== 'cancelled' && s !== 'integrated';
  }

  canDeleteReturn(r: SalesReturn): boolean {
    if (!r.id || !this.perm.has(Permissions.SalesReturnUpdate)) return false;
    const s = (r.status || '').toLowerCase();
    return s === 'cancelled' || s === 'draft';
  }

  canCreateCreditNoteForReturn(r: SalesReturn): boolean {
    if (!r.id || r.creditNoteId || !this.perm.has(Permissions.SalesReturnUpdate)) return false;
    // Avoir déjà présent côté liste avoirs (lien SalesReturnId) — détecté si creditNoteId hydraté
    if (this.creditNotes.some(cn =>
      cn.salesReturnId === r.id && (cn.status || '').toLowerCase() !== 'cancelled')) {
      return false;
    }
    return (r.status || '').toLowerCase() === 'integrated';
  }

  creditNoteLabelForReturn(r: SalesReturn): string {
    if (!r.creditNoteId) return '';
    const cn = this.creditNotes.find(c => c.id === r.creditNoteId);
    return cn?.creditNoteNumber || `#${r.creditNoteId}`;
  }

  receiveSalesReturn(r: SalesReturn): void {
    if (!r.id || !this.canReceiveReturn(r)) return;
    this.actionError = '';
    this.businessService.receiveSalesReturn(r.id).subscribe({
      next: (updated) => {
        this.actionMessage = this.i18n.t('sales.returns.received', { number: updated.returnNumber });
        this.loadReturns();
      },
      error: (err) => {
        this.actionError = err?.error?.error || err?.error || this.i18n.t('sales.returns.error');
      }
    });
  }

  controlSalesReturn(r: SalesReturn): void {
    if (!r.id || !this.canControlReturn(r)) return;
    const quality = prompt(this.i18n.t('sales.returns.qualityPrompt'), 'Conforme') || undefined;
    this.actionError = '';
    this.businessService.controlSalesReturn(r.id, quality).subscribe({
      next: (updated) => {
        this.actionMessage = this.i18n.t('sales.returns.controlled', { number: updated.returnNumber });
        this.loadReturns();
      },
      error: (err) => {
        this.actionError = err?.error?.error || err?.error || this.i18n.t('sales.returns.error');
      }
    });
  }

  integrateSalesReturn(r: SalesReturn): void {
    if (!r.id || !this.canIntegrateReturn(r)) return;
    const createCreditNote = confirm(this.i18n.t('sales.returns.integrateCreditNotePrompt'));
    this.actionError = '';
    this.businessService.integrateSalesReturn(r.id, createCreditNote).subscribe({
      next: (res: any) => {
        this.actionMessage = this.i18n.t('sales.returns.integrated', { number: r.returnNumber });
        // Stock OK même si l'avoir échoue (plafond facture, etc.).
        const creditErr = res?.creditNoteError || res?.CreditNoteError;
        if (creditErr) {
          this.actionError = typeof creditErr === 'string'
            ? creditErr
            : this.i18n.t('sales.returns.creditNoteAfterStockError');
        } else if (createCreditNote && (res?.creditNote || res?.CreditNote)) {
          const cn = res.creditNote || res.CreditNote;
          const notes = (cn.notes || cn.Notes || '') as string;
          const capped = /plafonn|capacité restante|capped/i.test(notes);
          this.actionMessage = capped
            ? this.i18n.t('sales.returns.integratedWithCreditCapped', {
                number: r.returnNumber,
                credit: cn.creditNoteNumber || cn.CreditNoteNumber || '',
                amount: (cn.totalTTC ?? cn.TotalTTC ?? 0).toFixed(2)
              })
            : this.i18n.t('sales.returns.integratedWithCredit', {
                number: r.returnNumber,
                credit: cn.creditNoteNumber || cn.CreditNoteNumber || ''
              });
          this.businessService.getCreditNotes().subscribe(list => this.creditNotes = list);
        }
        this.loadReturns();
      },
      error: (err) => {
        this.actionError = err?.error?.error || err?.error || this.i18n.t('sales.returns.error');
      }
    });
  }

  cancelSalesReturn(r: SalesReturn): void {
    if (!r.id || !this.canCancelReturn(r)) return;
    const reason = prompt(this.i18n.t('sales.cancelReasonPrompt'));
    if (reason == null) return;
    this.actionError = '';
    this.businessService.cancelSalesReturn(r.id, reason.trim() || undefined).subscribe({
      next: (updated) => {
        this.actionMessage = this.i18n.t('sales.returns.cancelled', { number: updated.returnNumber });
        this.loadReturns();
      },
      error: (err) => {
        this.actionError = err?.error?.error || err?.error || this.i18n.t('sales.returns.error');
      }
    });
  }

  deleteSalesReturn(r: SalesReturn): void {
    if (!r.id || !this.canDeleteReturn(r)) return;
    if (!confirm(this.i18n.t('sales.returns.deleteConfirm', { number: r.returnNumber }))) return;
    this.actionError = '';
    this.businessService.deleteSalesReturn(r.id).subscribe({
      next: () => {
        this.actionMessage = this.i18n.t('sales.returns.deleted', { number: r.returnNumber });
        this.loadReturns();
      },
      error: (err) => {
        this.actionError = err?.error?.error || err?.error || this.i18n.t('sales.returns.error');
      }
    });
  }

  createCreditNoteFromReturn(r: SalesReturn): void {
    if (!r.id || !this.canCreateCreditNoteForReturn(r)) return;
    this.actionError = '';
    this.businessService.createCreditNoteFromReturn(r.id).subscribe({
      next: (creditNote) => {
        const notes = creditNote.notes || '';
        const capped = /plafonn|capacité restante|capped/i.test(notes);
        this.actionMessage = capped
          ? this.i18n.t('sales.returns.creditNoteCreatedCapped', {
              number: creditNote.creditNoteNumber,
              amount: (creditNote.totalTTC || 0).toFixed(2)
            })
          : this.i18n.t('sales.returns.creditNoteCreated', { number: creditNote.creditNoteNumber });
        this.loadReturns();
        this.businessService.getCreditNotes().subscribe(cn => this.creditNotes = cn);
      },
      error: (err) => {
        this.actionError = err?.error?.error || err?.error || this.i18n.t('sales.returns.error');
      }
    });
  }

  // ── Proformas ─────────────────────────────────────────────────────────────────

  openProformaModal(): void {
    if (!this.perm.has(Permissions.InvoiceCreate)) return;
    this.proformaQuoteId = null;
    this.actionError = '';
    this.showProformaModal = true;
  }

  submitProforma(): void {
    if (!this.proformaQuoteId) {
      this.actionError = this.i18n.t('sales.proformas.selectQuoteError');
      return;
    }
    this.saving = true;
    this.actionError = '';
    this.businessService.createProforma({ quoteId: this.proformaQuoteId, customerId: 0 }).subscribe({
      next: (created) => {
        this.saving = false;
        this.finishModalSuccess(
          () => { this.showProformaModal = false; },
          this.i18n.t('sales.proformas.created', { number: created.proformaNumber })
        );
      },
      error: (err) => {
        this.saving = false;
        this.actionError = err?.error?.error || err?.error || this.i18n.t('sales.proformas.error');
      }
    });
  }

  canSendProforma(p: Proforma): boolean {
    return !!p.id && this.perm.has(Permissions.InvoiceUpdate) && (p.status || '').toLowerCase() === 'draft';
  }

  canCancelProforma(p: Proforma): boolean {
    return !!p.id && this.perm.has(Permissions.InvoiceUpdate) && (p.status || '').toLowerCase() !== 'cancelled';
  }

  canDeleteProforma(p: Proforma): boolean {
    return !!p.id && this.perm.has(Permissions.InvoiceUpdate) && (p.status || '').toLowerCase() === 'draft';
  }

  sendProforma(p: Proforma): void {
    if (!p.id || !this.canSendProforma(p)) return;
    this.actionError = '';
    this.businessService.sendProforma(p.id).subscribe({
      next: (updated) => {
        this.actionMessage = this.i18n.t('sales.proformas.sent', { number: updated.proformaNumber });
        this.loadProformas();
      },
      error: (err) => {
        this.actionError = err?.error?.error || err?.error || this.i18n.t('sales.proformas.error');
      }
    });
  }

  cancelProforma(p: Proforma): void {
    if (!p.id || !this.canCancelProforma(p)) return;
    const reason = prompt(this.i18n.t('sales.cancelReasonPrompt'));
    if (reason == null) return;
    this.actionError = '';
    this.businessService.cancelProforma(p.id, reason.trim() || undefined).subscribe({
      next: (updated) => {
        this.actionMessage = this.i18n.t('sales.proformas.cancelled', { number: updated.proformaNumber });
        this.loadProformas();
      },
      error: (err) => {
        this.actionError = err?.error?.error || err?.error || this.i18n.t('sales.proformas.error');
      }
    });
  }

  deleteProforma(p: Proforma): void {
    if (!p.id || !this.canDeleteProforma(p)) return;
    if (!confirm(this.i18n.t('sales.proformas.confirmDelete', { number: p.proformaNumber }))) return;
    this.actionError = '';
    this.businessService.deleteProforma(p.id).subscribe({
      next: () => {
        this.actionMessage = this.i18n.t('sales.proformas.deleted', { number: p.proformaNumber });
        this.proformas = this.proformas.filter(x => x.id !== p.id);
      },
      error: (err) => {
        this.actionError = err?.error?.error || err?.error || this.i18n.t('sales.proformas.error');
      }
    });
  }

  // ── Acomptes (AAC) ────────────────────────────────────────────────────────────

  get ordersForDeposit(): SalesOrder[] {
    // Confirmed / PartiallyDelivered : OK.
    // Closed : uniquement s'il reste une facture non soldée (sinon acompte inutile).
    return this.orders.filter(o => this.isOrderEligibleForDeposit(o));
  }

  isOrderEligibleForDeposit(order: SalesOrder): boolean {
    const s = (order.status || '').toLowerCase();
    if (s === 'confirmed' || s === 'partiallydelivered') return true;
    if (s === 'closed') return this.orderHasUnsettledInvoice(order.id);
    return false;
  }

  /** Closed + facture(s) déjà payée(s) / soldée(s) → pas de nouvel acompte. */
  private orderHasUnsettledInvoice(orderId: number | undefined): boolean {
    if (!orderId) return false;
    return this.invoices.some(inv => {
      if (inv.salesOrderId !== orderId) return false;
      const st = (inv.status || '').toLowerCase();
      if (st === 'cancelled' || st === 'draft') return false;
      if (st === 'paid') return false;
      return this.invoiceRemaining(inv) > 0.01;
    });
  }

  openDepositModal(): void {
    if (!this.perm.has(Permissions.InvoiceCreate)) return;
    this.depositOrderId = null;
    this.depositAmountHT = 0;
    this.depositVatRate = 21;
    this.actionError = '';
    this.showDepositModal = true;
  }

  submitDeposit(): void {
    const order = this.orders.find(o => o.id === this.depositOrderId);
    if (!order) {
      this.actionError = this.i18n.t('sales.deposits.selectOrderError');
      return;
    }
    if (!this.depositAmountHT || this.depositAmountHT <= 0) {
      this.actionError = this.i18n.t('sales.deposits.amountError');
      return;
    }
    this.saving = true;
    this.actionError = '';
    this.businessService.createDepositInvoice({
      customerId: order.customerId,
      salesOrderId: order.id!,
      amountHT: this.depositAmountHT,
      vatRate: this.depositVatRate,
      amountTTC: 0,
      status: 'Draft',
      date: new Date().toISOString(),
      depositNumber: ''
    }).subscribe({
      next: (created) => {
        this.saving = false;
        this.finishModalSuccess(
          () => { this.showDepositModal = false; },
          this.i18n.t('sales.deposits.created', { number: created.depositNumber })
        );
      },
      error: (err) => {
        this.saving = false;
        this.actionError = err?.error?.error || err?.error || this.i18n.t('sales.deposits.error');
      }
    });
  }

  canValidateDeposit(d: DepositInvoice): boolean {
    return !!d.id && this.perm.has(Permissions.InvoiceUpdate) && (d.status || '').toLowerCase() === 'draft';
  }

  canApplyDeposit(d: DepositInvoice): boolean {
    return !!d.id && this.perm.has(Permissions.InvoiceUpdate) && (d.status || '').toLowerCase() === 'validated';
  }

  canCancelDeposit(d: DepositInvoice): boolean {
    if (!d.id || !this.perm.has(Permissions.InvoiceUpdate)) return false;
    const s = (d.status || '').toLowerCase();
    return s !== 'applied' && s !== 'cancelled';
  }

  validateDepositInvoice(d: DepositInvoice): void {
    if (!d.id || !this.canValidateDeposit(d)) return;
    this.actionError = '';
    this.businessService.validateDepositInvoice(d.id).subscribe({
      next: (updated) => {
        this.actionMessage = this.i18n.t('sales.deposits.validated', { number: updated.depositNumber });
        this.refreshMainAfterModal();
      },
      error: (err) => {
        this.actionError = err?.error?.error || err?.error || this.i18n.t('sales.deposits.error');
      }
    });
  }

  get invoicesForDepositApply(): SalesInvoice[] {
    if (!this.depositToApply) return [];
    return this.invoices.filter(i => {
      if (i.customerId !== this.depositToApply!.customerId) return false;
      if (this.depositToApply!.salesOrderId && i.salesOrderId !== this.depositToApply!.salesOrderId) return false;
      const s = (i.status || '').toLowerCase();
      if (s === 'draft' || s === 'cancelled' || s === 'paid') return false;
      return this.invoiceRemaining(i) > 0.01;
    });
  }

  openApplyDepositModal(d: DepositInvoice): void {
    if (!d.id || !this.canApplyDeposit(d)) return;
    this.depositToApply = d;
    this.applyDepositInvoiceId = null;
    this.actionError = '';
    this.showApplyDepositModal = true;
  }

  submitApplyDeposit(): void {
    if (!this.depositToApply?.id || !this.applyDepositInvoiceId) {
      this.actionError = this.i18n.t('sales.deposits.selectInvoiceError');
      return;
    }
    this.saving = true;
    this.actionError = '';
    this.businessService.applyDepositToInvoice(this.depositToApply.id, this.applyDepositInvoiceId).subscribe({
      next: (updated) => {
        this.saving = false;
        this.finishModalSuccess(
          () => { this.showApplyDepositModal = false; },
          this.i18n.t('sales.deposits.applied', { number: updated.depositNumber })
        );
      },
      error: (err) => {
        this.saving = false;
        this.actionError = err?.error?.error || err?.error || this.i18n.t('sales.deposits.error');
      }
    });
  }

  cancelDepositInvoice(d: DepositInvoice): void {
    if (!d.id || !this.canCancelDeposit(d)) return;
    const reason = prompt(this.i18n.t('sales.cancelReasonPrompt'));
    if (reason == null) return;
    this.actionError = '';
    this.businessService.cancelDepositInvoice(d.id, reason.trim() || undefined).subscribe({
      next: (updated) => {
        this.actionMessage = this.i18n.t('sales.deposits.cancelled', { number: updated.depositNumber });
        this.refreshMainAfterModal();
      },
      error: (err) => {
        this.actionError = err?.error?.error || err?.error || this.i18n.t('sales.deposits.error');
      }
    });
  }
}
