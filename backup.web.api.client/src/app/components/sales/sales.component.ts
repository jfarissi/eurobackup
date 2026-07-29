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
  SalesOrderLine
} from '../../models/business';
import { Observable } from 'rxjs';
import { ProductLineRefComponent } from '../shared/product-line-ref/product-line-ref.component';
import { PermissionService } from '../../services/permission.service';
import { Permissions } from '../../constants/permissions';
import { HasPermissionDirective } from '../../directives/has-permission.directive';
import { AppI18nService } from '../../services/app-i18n.service';
import { TPipe } from '../../pipes/t.pipe';

type DocKind = 'Quote' | 'Order' | 'Invoice';

interface DocLineDraft {
  productKey: string;
  description: string;
  quantity: number;
  unitPrice: number;
  vatRate: number;
  totalHT: number;
  totalTTC: number;
  lineNumber: number;
  deliveredQuantity?: number;
}

@Component({
  selector: 'app-sales',
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, ProductLineRefComponent, HasPermissionDirective, TPipe],
  templateUrl: './sales.component.html',
  styleUrls: ['./sales.component.css']
})
export class SalesComponent implements OnInit {
  selectedTab = 0; // 0: Invoices, 1: Orders, 2: Quotes, 3: Credit notes, 4: DeliveryNotes, 5: Customers

  invoices: SalesInvoice[] = [];
  orders: SalesOrder[] = [];
  quotes: Quote[] = [];
  creditNotes: CreditNote[] = [];
  deliveryNotes: SalesDeliveryNote[] = [];
  customers: Customer[] = [];

  loading = false;
  saving = false;
  searchQuery = '';
  actionMessage = '';
  actionError = '';

  showDocModal = false;
  docKind: DocKind = 'Invoice';
  docCustomerId: number | null = null;
  docNotes = '';
  docExpirationDate = '';
  docDueDate = '';
  docLines: DocLineDraft[] = [];

  showCustomerModal = false;
  editingCustomerId: number | null = null;
  newCustomer: Partial<Customer> = this.emptyCustomer();
  openCustomerFromDoc = false;

  showPaymentModal = false;
  selectedInvoiceForPayment: SalesInvoice | null = null;
  paymentAmount = 0;
  paymentMethod = 'Cash';

  expandedRowKey: string | null = null;
  detailLoading = false;
  pdfDownloading = false;
  detailKind: 'Quote' | 'Order' | 'Invoice' | 'CreditNote' | 'DeliveryNote' | null = null;
  detailQuote: Quote | null = null;
  detailOrder: SalesOrder | null = null;
  detailInvoice: SalesInvoice | null = null;
  detailCreditNote: CreditNote | null = null;
  detailDeliveryNote: SalesDeliveryNote | null = null;

  // BL creation form
  showDeliveryNoteModal = false;
  dnCustomerId: number | null = null;
  dnSalesOrderId: number | null = null;
  dnDate = '';
  dnNotes = '';
  dnLines: { productKey: string; description: string; orderedQuantity: number; deliveredQuantity: number; unitPrice: number; vatRate: number; totalHT: number; totalTTC: number; lineNumber: number }[] = [];

  readonly P = Permissions;

  constructor(
    private businessService: BusinessService,
    public perm: PermissionService,
    private i18n: AppI18nService
  ) {}

  ngOnInit(): void {
    this.loadAllData();
  }

  get createButtonLabel(): string {
    switch (this.selectedTab) {
      case 1: return this.i18n.t('sales.btn.newOrder');
      case 2: return this.i18n.t('sales.btn.newQuote');
      case 4: return this.i18n.t('sales.btn.newDeliveryNote');
      case 5: return this.i18n.t('sales.btn.newCustomer');
      default: return this.i18n.t('sales.btn.newInvoice');
    }
  }

  get docModalTitle(): string {
    switch (this.docKind) {
      case 'Quote': return this.i18n.t('sales.modal.newQuote');
      case 'Order': return this.i18n.t('sales.modal.newOrder');
      default: return this.i18n.t('sales.modal.newInvoice');
    }
  }

  get docTotals(): { ht: number; vat: number; ttc: number } {
    const ht = this.docLines.reduce((sum, l) => sum + (l.totalHT || 0), 0);
    const vat = this.docLines.reduce((sum, l) => sum + (l.totalHT || 0) * ((l.vatRate || 0) / 100), 0);
    return { ht, vat, ttc: ht + vat };
  }

  canCreateOnTab(): boolean {
    switch (this.selectedTab) {
      case 0: return this.perm.has(Permissions.InvoiceCreate);
      case 1: return this.perm.has(Permissions.OrderCreate);
      case 2: return this.perm.has(Permissions.QuoteCreate);
      case 4: return this.perm.has(Permissions.DeliveryNoteCreate);
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
    this.businessService.getSalesInvoices().subscribe(i => {
      this.invoices = i;
      this.loading = false;
    });
  }

  onSearch(): void {
    if (this.selectedTab === 0) {
      this.businessService.getSalesInvoices(this.searchQuery || undefined).subscribe(res => this.invoices = res);
    } else if (this.selectedTab === 1) {
      this.businessService.getSalesOrders(this.searchQuery || undefined).subscribe(res => this.orders = res);
    } else if (this.selectedTab === 2) {
      this.businessService.getQuotes(this.searchQuery || undefined).subscribe(res => this.quotes = res);
    } else if (this.selectedTab === 3) {
      this.businessService.getCreditNotes(this.searchQuery || undefined).subscribe(res => this.creditNotes = res);
    } else if (this.selectedTab === 4) {
      this.businessService.getSalesDeliveryNotes(this.searchQuery || undefined).subscribe(res => this.deliveryNotes = res);
    } else if (this.selectedTab === 5) {
      this.businessService.getCustomers(this.searchQuery || undefined).subscribe(res => this.customers = res);
    }
  }

  onCreateClick(): void {
    if (this.selectedTab === 5) {
      this.openCustomerModal();
      return;
    }
    if (this.selectedTab === 4) {
      this.openDeliveryNoteModal();
      return;
    }
    if (this.customers.length === 0) {
      this.actionError = this.i18n.t('sales.needCustomerFirst');
      this.selectedTab = 4;
      return;
    }
    if (this.selectedTab === 1) this.openDocModal('Order');
    else if (this.selectedTab === 2) this.openDocModal('Quote');
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
    this.docCustomerId = null;
    this.docNotes = '';
    this.docLines = [this.emptyLine(1)];
    const today = new Date();
    const inThirty = new Date(today.getTime() + 30 * 24 * 60 * 60 * 1000);
    this.docExpirationDate = this.toDateInput(inThirty);
    this.docDueDate = this.toDateInput(inThirty);
    this.actionError = '';
    this.showDocModal = true;
  }

  addDocLine(): void {
    this.docLines.push(this.emptyLine(this.docLines.length + 1));
  }

  removeDocLine(index: number): void {
    this.docLines.splice(index, 1);
    this.docLines.forEach((line, i) => line.lineNumber = i + 1);
  }

  calcLine(line: DocLineDraft): void {
    line.totalHT = +(Number(line.quantity || 0) * Number(line.unitPrice || 0)).toFixed(2);
    line.totalTTC = +(line.totalHT * (1 + Number(line.vatRate || 0) / 100)).toFixed(2);
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
        next: (created) => this.onDocCreated(this.i18n.t('sales.quoteCreated', { number: created.quoteNumber }), 2),
        error: (error) => this.onDocError(error, this.i18n.t('sales.action.createQuote'))
      });
      return;
    }

    if (this.docKind === 'Order') {
      const order: SalesOrder = {
        orderNumber: '',
        customerId: this.docCustomerId,
        date: new Date().toISOString(),
        status: 'Confirmed',
        totalHT: this.docTotals.ht,
        totalVat: this.docTotals.vat,
        totalTTC: this.docTotals.ttc,
        notes: this.docNotes || undefined,
        lines: this.docLines.map((l, i) => ({
          productKey: l.productKey,
          description: l.description,
          quantity: l.quantity,
          deliveredQuantity: 0,
          unitPrice: l.unitPrice,
          vatRate: l.vatRate,
          totalHT: l.totalHT,
          totalTTC: l.totalTTC,
          lineNumber: i + 1
        } as SalesOrderLine))
      };
      this.businessService.createSalesOrder(order).subscribe({
        next: (created) => this.onDocCreated(this.i18n.t('sales.orderCreated', { number: created.orderNumber }), 1),
        error: (error) => this.onDocError(error, this.i18n.t('sales.action.createOrder'))
      });
      return;
    }

    const invoice: SalesInvoice = {
      invoiceNumber: '',
      customerId: this.docCustomerId,
      date: new Date().toISOString(),
      dueDate: this.docDueDate ? new Date(this.docDueDate).toISOString() : new Date().toISOString(),
      status: 'Draft',
      totalHT: this.docTotals.ht,
      totalVat: this.docTotals.vat,
      totalTTC: this.docTotals.ttc,
      paidAmount: 0,
      notes: this.docNotes || undefined,
      lines: this.docLines.map((l, i) => ({ ...l, lineNumber: i + 1 } as SalesInvoiceLine))
    };
    this.businessService.createSalesInvoice(invoice).subscribe({
      next: (created) => this.onDocCreated(this.i18n.t('sales.invoiceCreated', { number: created.invoiceNumber }), 0),
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
      balance: customer.balance
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
      balance: 0
    };
    const request = this.editingCustomerId
      ? this.businessService.updateCustomer(this.editingCustomerId, { ...payload, id: this.editingCustomerId, balance: this.newCustomer.balance ?? 0 })
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
        this.businessService.getCustomers().subscribe(c => {
          this.customers = c;
          if (!wasEdit && fromDoc && saved.id) {
            this.docCustomerId = saved.id;
            this.showDocModal = true;
          } else if (!wasEdit) {
            this.selectedTab = 4;
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

  convertOrderToInvoice(order: SalesOrder): void {
    if (!order.id) return;
    this.actionError = '';
    this.businessService.convertToInvoice(order.id).subscribe({
      next: (invoice) => {
        this.selectedTab = 0;
        this.actionMessage = this.i18n.t('sales.invoiceFromOrder', { invoice: invoice.invoiceNumber, order: order.orderNumber });
        this.loadAllData();
      },
      error: (error) => {
        this.actionError = error?.error?.error || error?.error || this.i18n.t('sales.orderToInvoiceError');
      }
    });
  }

  openPaymentModal(invoice: SalesInvoice): void {
    this.selectedInvoiceForPayment = invoice;
    this.paymentAmount = +(invoice.totalTTC - invoice.paidAmount).toFixed(2);
    this.paymentMethod = 'Cash';
    this.showPaymentModal = true;
  }

  submitPayment(): void {
    if (!this.selectedInvoiceForPayment?.id) return;
    this.actionError = '';
    this.businessService.recordPayment(this.selectedInvoiceForPayment.id, this.paymentAmount, this.paymentMethod)
      .subscribe({
        next: () => {
          this.showPaymentModal = false;
          this.actionMessage = this.i18n.t('sales.paymentSaved', { amount: this.paymentAmount.toFixed(2) });
          this.loadAllData();
        },
        error: (error) => {
          this.actionError = error?.error?.error || error?.error || this.i18n.t('sales.paymentError');
        }
      });
  }

  createCreditNoteFromInvoice(invoice: SalesInvoice): void {
    if (!invoice.id) return;
    this.actionError = '';
    this.businessService.createCreditNoteFromInvoice(invoice.id).subscribe({
      next: (creditNote) => {
        this.selectedTab = 3;
        this.actionMessage = this.i18n.t('sales.creditNoteFromInvoice', { creditNote: creditNote.creditNoteNumber, invoice: invoice.invoiceNumber });
        this.loadAllData();
      },
      error: (error) => {
        this.actionError = error?.error?.error || error?.error || this.i18n.t('sales.creditNoteCreateError');
      }
    });
  }

  validateCreditNote(creditNote: CreditNote): void {
    if (!creditNote.id) return;
    this.actionError = '';
    this.businessService.validateCreditNote(creditNote.id).subscribe({
      next: (updated) => {
        this.actionMessage = this.i18n.t('sales.creditNoteValidated', { number: updated.creditNoteNumber });
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
        else if (kind === 'Order') this.detailOrder = full as SalesOrder;
        else if (kind === 'Invoice') this.detailInvoice = full as SalesInvoice;
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

  get detailLines(): Array<{ productKey: string; description: string; quantity: number; unitPrice: number; vatRate: number; totalHT: number; totalTTC: number; extra?: string }> {
    if (this.detailQuote?.lines) {
      return this.detailQuote.lines.map(l => ({ ...l }));
    }
    if (this.detailOrder?.lines) {
      return this.detailOrder.lines.map(l => ({
        ...l,
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

  get detailTotals(): { ht: number; vat: number; ttc: number; paid?: number; status?: string; date?: string; notes?: string } {
    const doc = this.detailQuote || this.detailOrder || this.detailInvoice || this.detailCreditNote;
    if (doc) return {
      ht: doc.totalHT,
      vat: doc.totalVat,
      ttc: doc.totalTTC,
      paid: this.detailInvoice?.paidAmount,
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
    this.actionError = '';
  }

  private onDocCreated(message: string, tab: number): void {
    this.saving = false;
    this.showDocModal = false;
    this.selectedTab = tab;
    this.actionMessage = message;
    this.loadAllData();
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
      this.dnCustomerId = this.customers[0]?.id ?? null;
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
    const note: SalesDeliveryNote = {
      deliveryNumber: '',
      customerId: this.dnCustomerId,
      salesOrderId: this.dnSalesOrderId ?? undefined,
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
        this.showDeliveryNoteModal = false;
        this.actionMessage = this.i18n.t('sales.deliveryNoteCreated', { number: created.deliveryNumber });
        this.deliveryNotes = [created, ...this.deliveryNotes];
      },
      error: (err) => {
        this.saving = false;
        this.actionError = err?.error?.error || err?.error || this.i18n.t('sales.deliveryNoteCreateError');
      }
    });
  }

  createDeliveryNoteFromOrder(order: SalesOrder): void {
    if (!this.perm.has(Permissions.DeliveryNoteCreate)) return;
    this.saving = true;
    this.businessService.createSalesDeliveryNoteFromOrder(order.id!).subscribe({
      next: (note) => {
        this.saving = false;
        this.actionMessage = this.i18n.t('sales.deliveryNoteFromOrder', { delivery: note.deliveryNumber, order: order.orderNumber });
        this.businessService.getSalesDeliveryNotes().subscribe(d => this.deliveryNotes = d);
        this.businessService.getSalesOrders().subscribe(o => this.orders = o);
      },
      error: (err) => {
        this.saving = false;
        this.actionError = err?.error?.error || err?.error || this.i18n.t('sales.genericError');
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
      balance: 0
    };
  }

  private toDateInput(date: Date): string {
    return date.toISOString().slice(0, 10);
  }
}
