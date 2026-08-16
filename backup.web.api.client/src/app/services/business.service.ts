import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  Customer,
  Supplier,
  Quote,
  SalesOrder,
  SalesInvoice,
  SalesDeliveryNote,
  CreditNote,
  PurchaseOrder,
  DropshipPurchaseOrder,
  ReceiveDeliveryResult,
  Receipt,
  ComptabiliserResult,
  ComptabiliserInvoiceResult,
  SupplierInvoice,
  SupplierPayment,
  UnifiedPayment,
  SupplierInvoicePurchaseOrderMatchResult,
  StockMovement,
  CashSession,
  CashOperation,
  DocumentNumberSequence,
  AccountingEntry,
  ManualAccountingEntryRequest,
  Payment,
  RecordPaymentResult,
  SalesPilotage,
  DocumentAuditLog,
  SalesTrashItem,
  SalesReturn,
  Proforma,
  DepositInvoice,
  SupplierRfq,
  SupplierReturn,
  SupplierCreditNote
} from '../models/business';

@Injectable({
  providedIn: 'root'
})
export class BusinessService {
  constructor(private http: HttpClient) {}

  // Customers
  getCustomers(search?: string): Observable<Customer[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    return this.http.get<Customer[]>('/api/customers', { params });
  }

  getCustomer(id: number): Observable<Customer> {
    return this.http.get<Customer>(`/api/customers/${id}`);
  }

  createCustomer(customer: Customer): Observable<Customer> {
    return this.http.post<Customer>('/api/customers', customer);
  }

  updateCustomer(id: number, customer: Customer): Observable<Customer> {
    return this.http.put<Customer>(`/api/customers/${id}`, customer);
  }

  deleteCustomer(id: number): Observable<void> {
    return this.http.delete<void>(`/api/customers/${id}`);
  }

  // Suppliers
  getSuppliers(search?: string): Observable<Supplier[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    return this.http.get<Supplier[]>('/api/suppliers', { params });
  }

  getSupplier(id: number): Observable<Supplier> {
    return this.http.get<Supplier>(`/api/suppliers/${id}`);
  }

  createSupplier(supplier: Supplier): Observable<Supplier> {
    return this.http.post<Supplier>('/api/suppliers', supplier);
  }

  updateSupplier(id: number, supplier: Supplier): Observable<Supplier> {
    return this.http.put<Supplier>(`/api/suppliers/${id}`, supplier);
  }

  deleteSupplier(id: number): Observable<void> {
    return this.http.delete<void>(`/api/suppliers/${id}`);
  }

  // Quotes
  getQuotes(search?: string): Observable<Quote[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    return this.http.get<Quote[]>('/api/quotes', { params });
  }

  getQuote(id: number): Observable<Quote> {
    return this.http.get<Quote>(`/api/quotes/${id}`);
  }

  createQuote(quote: Quote): Observable<Quote> {
    return this.http.post<Quote>('/api/quotes', quote);
  }

  updateQuote(id: number, quote: Quote): Observable<Quote> {
    return this.http.put<Quote>(`/api/quotes/${id}`, quote);
  }

  convertToOrder(quoteId: number): Observable<SalesOrder> {
    return this.http.post<SalesOrder>(`/api/quotes/${quoteId}/convert-to-order`, {});
  }

  acceptQuote(quoteId: number): Observable<Quote> {
    return this.http.post<Quote>(`/api/quotes/${quoteId}/accept`, {});
  }

  deleteQuote(id: number): Observable<void> {
    return this.http.delete<void>(`/api/quotes/${id}`);
  }

  // Sales Orders
  getSalesOrders(search?: string): Observable<SalesOrder[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    return this.http.get<SalesOrder[]>('/api/salesorders', { params });
  }

  getSalesOrder(id: number): Observable<SalesOrder> {
    return this.http.get<SalesOrder>(`/api/salesorders/${id}`);
  }

  getDropshipPurchaseOrders(salesOrderId: number): Observable<DropshipPurchaseOrder[]> {
    return this.http.get<DropshipPurchaseOrder[]>(`/api/salesorders/${salesOrderId}/dropship-pos`);
  }

  createSalesOrder(order: SalesOrder): Observable<SalesOrder> {
    return this.http.post<SalesOrder>('/api/salesorders', order);
  }

  updateSalesOrder(id: number, order: SalesOrder): Observable<SalesOrder> {
    return this.http.put<SalesOrder>(`/api/salesorders/${id}`, order);
  }

  cancelSalesOrder(id: number, reason: string): Observable<SalesOrder> {
    return this.http.post<SalesOrder>(`/api/salesorders/${id}/cancel`, { reason });
  }

  confirmSalesOrder(id: number): Observable<SalesOrder> {
    return this.http.post<SalesOrder>(`/api/salesorders/${id}/confirm`, {});
  }

  approveSalesOrder(id: number): Observable<SalesOrder> {
    return this.http.post<SalesOrder>(`/api/salesorders/${id}/approve`, {});
  }

  holdSalesOrder(id: number, reason?: string): Observable<SalesOrder> {
    return this.http.post<SalesOrder>(`/api/salesorders/${id}/hold`, { reason: reason || null });
  }

  getSalesPilotage(): Observable<SalesPilotage> {
    return this.http.get<SalesPilotage>('/api/salesorders/pilotage');
  }

  archiveSalesOrder(id: number): Observable<SalesOrder> {
    return this.http.post<SalesOrder>(`/api/salesorders/${id}/archive`, {});
  }

  getSalesOrderAudit(id: number): Observable<DocumentAuditLog[]> {
    return this.http.get<DocumentAuditLog[]>(`/api/salesorders/${id}/audit`);
  }

  getSalesTrash(search?: string): Observable<SalesTrashItem[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    return this.http.get<SalesTrashItem[]>('/api/sales/trash', { params });
  }

  restoreSalesTrashItem(documentType: string, id: number): Observable<unknown> {
    return this.http.post(`/api/sales/trash/${encodeURIComponent(documentType)}/${id}/restore`, {});
  }

  purgeSalesTrashItem(documentType: string, id: number): Observable<void> {
    return this.http.delete<void>(`/api/sales/trash/${encodeURIComponent(documentType)}/${id}`);
  }

  emptySalesTrash(): Observable<{ purged: number }> {
    return this.http.delete<{ purged: number }>('/api/sales/trash');
  }

  convertToInvoice(orderId: number): Observable<SalesInvoice> {
    return this.http.post<SalesInvoice>(`/api/salesorders/${orderId}/convert-to-invoice`, {});
  }

  // Sales Invoices
  getSalesInvoices(search?: string): Observable<SalesInvoice[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    return this.http.get<SalesInvoice[]>('/api/salesinvoices', { params });
  }

  getSalesInvoice(id: number): Observable<SalesInvoice> {
    return this.http.get<SalesInvoice>(`/api/salesinvoices/${id}`);
  }

  createSalesInvoice(invoice: SalesInvoice): Observable<SalesInvoice> {
    return this.http.post<SalesInvoice>('/api/salesinvoices', invoice);
  }

  updateSalesInvoice(id: number, invoice: SalesInvoice): Observable<SalesInvoice> {
    return this.http.put<SalesInvoice>(`/api/salesinvoices/${id}`, invoice);
  }

  deleteSalesInvoice(id: number): Observable<void> {
    return this.http.delete<void>(`/api/salesinvoices/${id}`);
  }

  getSalesInvoiceAudit(id: number): Observable<DocumentAuditLog[]> {
    return this.http.get<DocumentAuditLog[]>(`/api/salesinvoices/${id}/audit`);
  }

  validateSalesInvoice(id: number): Observable<SalesInvoice> {
    return this.http.post<SalesInvoice>(`/api/salesinvoices/${id}/validate`, {});
  }

  getOverdueSalesInvoices(): Observable<SalesInvoice[]> {
    return this.http.get<SalesInvoice[]>('/api/salesinvoices/overdue');
  }

  remindSalesInvoice(id: number, sendEmail = true): Observable<{ invoice: SalesInvoice; email?: unknown }> {
    return this.http.post<{ invoice: SalesInvoice; email?: unknown }>(`/api/salesinvoices/${id}/remind`, { sendEmail });
  }

  recordPayment(
    invoiceId: number,
    amount: number,
    paymentMethod: string,
    notes?: string,
    extras?: { reference?: string; bank?: string; receivedAmount?: number; changeAmount?: number }
  ): Observable<RecordPaymentResult> {
    return this.http.post<RecordPaymentResult>(`/api/salesinvoices/${invoiceId}/pay`, {
      amount,
      paymentMethod,
      notes,
      reference: extras?.reference,
      bank: extras?.bank,
      receivedAmount: extras?.receivedAmount ?? 0,
      changeAmount: extras?.changeAmount ?? 0
    });
  }

  getPayments(salesInvoiceId?: number, status?: string): Observable<Payment[]> {
    let params = new HttpParams();
    if (salesInvoiceId) params = params.set('salesInvoiceId', String(salesInvoiceId));
    if (status) params = params.set('status', status);
    return this.http.get<Payment[]>('/api/payments', { params });
  }

  getUnifiedPayments(filters?: {
    side?: 'sales' | 'purchases' | 'all';
    status?: string;
    from?: string;
    to?: string;
    search?: string;
  }): Observable<UnifiedPayment[]> {
    let params = new HttpParams();
    if (filters?.side) params = params.set('side', filters.side);
    if (filters?.status) params = params.set('status', filters.status);
    if (filters?.from) params = params.set('from', filters.from);
    if (filters?.to) params = params.set('to', filters.to);
    if (filters?.search) params = params.set('search', filters.search);
    return this.http.get<UnifiedPayment[]>('/api/payments/all', { params });
  }

  validateSupplierInvoice(id: number): Observable<SupplierInvoice> {
    return this.http.post<SupplierInvoice>(`/api/supplierinvoices/${id}/validate`, {});
  }

  cancelPayment(paymentId: number): Observable<Payment> {
    return this.http.post<Payment>(`/api/payments/${paymentId}/cancel`, {});
  }

  getAccountingEntries(filters?: {
    referenceType?: string;
    referenceId?: number;
    journalType?: string;
    search?: string;
  }): Observable<AccountingEntry[]> {
    let params = new HttpParams();
    if (filters?.referenceType) params = params.set('referenceType', filters.referenceType);
    if (filters?.referenceId != null) params = params.set('referenceId', String(filters.referenceId));
    if (filters?.journalType) params = params.set('journalType', filters.journalType);
    if (filters?.search) params = params.set('search', filters.search);
    return this.http.get<AccountingEntry[]>('/api/accountingentries', { params });
  }

  getAccountingEntry(id: number): Observable<AccountingEntry> {
    return this.http.get<AccountingEntry>(`/api/accountingentries/${id}`);
  }

  createAccountingEntry(request: ManualAccountingEntryRequest): Observable<AccountingEntry> {
    return this.http.post<AccountingEntry>('/api/accountingentries', request);
  }

  // Credit Notes
  getCreditNotes(search?: string, customerId?: number, salesInvoiceId?: number): Observable<CreditNote[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    if (customerId) params = params.set('customerId', customerId);
    if (salesInvoiceId) params = params.set('salesInvoiceId', salesInvoiceId);
    return this.http.get<CreditNote[]>('/api/creditnotes', { params });
  }

  getCreditNote(id: number): Observable<CreditNote> {
    return this.http.get<CreditNote>(`/api/creditnotes/${id}`);
  }

  createCreditNote(creditNote: CreditNote): Observable<CreditNote> {
    return this.http.post<CreditNote>('/api/creditnotes', creditNote);
  }

  createCreditNoteFromInvoice(
    salesInvoiceId: number,
    notes?: string,
    lines?: { invoiceLineId?: number; lineNumber?: number; productKey?: string; quantity: number }[]
  ): Observable<CreditNote> {
    return this.http.post<CreditNote>('/api/creditnotes/from-invoice', { salesInvoiceId, notes, lines });
  }

  validateCreditNote(id: number): Observable<CreditNote> {
    return this.http.post<CreditNote>(`/api/creditnotes/${id}/validate`, {});
  }

  applyCreditNote(id: number): Observable<CreditNote> {
    return this.http.post<CreditNote>(`/api/creditnotes/${id}/apply`, {});
  }

  // Purchase Orders
  getPurchaseOrders(search?: string): Observable<PurchaseOrder[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    return this.http.get<PurchaseOrder[]>('/api/purchaseorders', { params });
  }

  getPurchaseOrder(id: number): Observable<PurchaseOrder> {
    return this.http.get<PurchaseOrder>(`/api/purchaseorders/${id}`);
  }

  createPurchaseOrder(order: PurchaseOrder): Observable<PurchaseOrder> {
    return this.http.post<PurchaseOrder>('/api/purchaseorders', order);
  }

  confirmPurchaseOrder(id: number): Observable<PurchaseOrder> {
    return this.http.post<PurchaseOrder>(`/api/purchaseorders/${id}/confirm`, {});
  }

  sendPurchaseOrder(id: number, sendEmail = true): Observable<{ purchaseOrder: PurchaseOrder; email?: unknown; emailWarning?: string }> {
    return this.http.post<{ purchaseOrder: PurchaseOrder; email?: unknown; emailWarning?: string }>(`/api/purchaseorders/${id}/send`, { sendEmail });
  }

  receivePurchaseOrderFromDelivery(orderId: number, deliveryDocumentId: number, updateStock = true): Observable<ReceiveDeliveryResult> {
    return this.http.post<ReceiveDeliveryResult>(`/api/purchaseorders/${orderId}/receive-delivery`, {
      deliveryDocumentId,
      updateStock
    });
  }

  // Supplier Invoices
  getSupplierInvoices(search?: string, supplierId?: number, documentId?: number): Observable<SupplierInvoice[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    if (supplierId) params = params.set('supplierId', supplierId);
    if (documentId) params = params.set('documentId', documentId);
    return this.http.get<SupplierInvoice[]>('/api/supplierinvoices', { params });
  }

  getSupplierInvoice(id: number): Observable<SupplierInvoice> {
    return this.http.get<SupplierInvoice>(`/api/supplierinvoices/${id}`);
  }

  createSupplierInvoice(invoice: SupplierInvoice): Observable<SupplierInvoice> {
    return this.http.post<SupplierInvoice>('/api/supplierinvoices', invoice);
  }

  updateSupplierInvoice(id: number, invoice: SupplierInvoice): Observable<SupplierInvoice> {
    return this.http.put<SupplierInvoice>(`/api/supplierinvoices/${id}`, invoice);
  }

  createSupplierInvoiceFromDocument(documentId: number, supplierId: number, companyId?: string, defaultVatRate = 21): Observable<SupplierInvoice> {
    return this.http.post<SupplierInvoice>('/api/supplierinvoices/from-document', {
      documentId,
      supplierId,
      companyId,
      defaultVatRate
    });
  }

  linkDocumentToSupplierInvoice(invoiceId: number, documentId: number): Observable<SupplierInvoice> {
    return this.http.post<SupplierInvoice>(`/api/supplierinvoices/${invoiceId}/link-document`, { documentId });
  }

  matchSupplierInvoiceToPurchaseOrder(invoiceId: number, purchaseOrderId: number): Observable<SupplierInvoicePurchaseOrderMatchResult> {
    return this.http.post<SupplierInvoicePurchaseOrderMatchResult>(`/api/supplierinvoices/${invoiceId}/match-purchase-order`, { purchaseOrderId });
  }

  previewSupplierInvoicePurchaseOrderMatch(invoiceId: number, purchaseOrderId: number): Observable<SupplierInvoicePurchaseOrderMatchResult> {
    return this.http.post<SupplierInvoicePurchaseOrderMatchResult>(`/api/supplierinvoices/${invoiceId}/preview-match-purchase-order`, { purchaseOrderId });
  }

  approveSupplierInvoice(invoiceId: number, reason?: string): Observable<SupplierInvoice> {
    return this.http.post<SupplierInvoice>(`/api/supplierinvoices/${invoiceId}/approve`, { reason });
  }

  getSupplierPayments(invoiceId: number): Observable<SupplierPayment[]> {
    return this.http.get<SupplierPayment[]>(`/api/supplierinvoices/${invoiceId}/payments`);
  }

  createSupplierPayment(
    invoiceId: number,
    body: { amount: number; paidAt?: string; method?: string; reference?: string }
  ): Observable<{ payment: SupplierPayment; invoice: SupplierInvoice }> {
    return this.http.post<{ payment: SupplierPayment; invoice: SupplierInvoice }>(
      `/api/supplierinvoices/${invoiceId}/payments`,
      body
    );
  }

  // Receipts (ErpReceipts) — BL comptabilisés
  getReceipts(search?: string, supplierId?: number): Observable<Receipt[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    if (supplierId) params = params.set('supplierId', supplierId);
    return this.http.get<Receipt[]>('/api/receipts', { params });
  }

  getReceipt(id: number): Observable<Receipt> {
    return this.http.get<Receipt>(`/api/receipts/${id}`);
  }

  getReceiptByDocument(documentId: number): Observable<Receipt> {
    return this.http.get<Receipt>(`/api/receipts/by-document/${documentId}`);
  }

  comptabiliserDeliveryNote(payload: {
    documentId: number;
    supplierId?: number;
    purchaseOrderId?: number;
    updateStock?: boolean;
    defaultVatRate?: number;
  }): Observable<ComptabiliserResult> {
    return this.http.post<ComptabiliserResult>('/api/receipts/comptabiliser', payload);
  }

  createReceipt(payload: {
    supplierId: number;
    purchaseOrderId?: number;
    receiptNumber?: string;
    receivedAt?: string;
    notes?: string;
    updateStock?: boolean;
    defaultVatRate?: number;
    lines: Array<{
      productKey: string;
      description?: string;
      quantityReceived: number;
      unitPriceExclTax: number;
      taxRatePercent?: number;
    }>;
  }): Observable<ComptabiliserResult> {
    return this.http.post<ComptabiliserResult>('/api/receipts', payload);
  }

  comptabiliserSupplierInvoice(payload: {
    documentId: number;
    supplierId?: number;
    purchaseOrderId?: number;
    companyId?: string;
    defaultVatRate?: number;
  }): Observable<ComptabiliserInvoiceResult> {
    return this.http.post<ComptabiliserInvoiceResult>('/api/supplierinvoices/comptabiliser', payload);
  }

  // Stock Movements
  getStockMovements(productKey?: string): Observable<StockMovement[]> {
    let params = new HttpParams();
    if (productKey) params = params.set('productKey', productKey);
    return this.http.get<StockMovement[]>('/api/stockmovements', { params });
  }

  createStockMovement(movement: StockMovement): Observable<StockMovement> {
    return this.http.post<StockMovement>('/api/stockmovements', movement);
  }

  // Cash Sessions
  getActiveCashSession(): Observable<CashSession | null> {
    return this.http.get<CashSession | null>('/api/cash/active-session');
  }

  getCashSessions(take = 50): Observable<CashSession[]> {
    return this.http.get<CashSession[]>('/api/cash/sessions', { params: { take: String(take) } });
  }

  getCashSessionById(id: number): Observable<CashSession> {
    return this.http.get<CashSession>(`/api/cash/sessions/${id}`);
  }

  openCashSession(openingBalance: number): Observable<CashSession> {
    return this.http.post<CashSession>('/api/cash/open-session', { openingBalance });
  }

  closeCashSession(sessionId: number, closingBalance: number): Observable<CashSession> {
    return this.http.post<CashSession>(`/api/cash/close-session/${sessionId}`, { closingBalance });
  }

  postCashOperation(op: Partial<CashOperation>): Observable<CashOperation> {
    return this.http.post<CashOperation>('/api/cash/operation', op);
  }

  // Sales Delivery Notes
  getSalesDeliveryNotes(search?: string, salesOrderId?: number): Observable<SalesDeliveryNote[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    if (salesOrderId != null) params = params.set('salesOrderId', String(salesOrderId));
    return this.http.get<SalesDeliveryNote[]>('/api/salesdeliverynotes', { params });
  }

  getSalesDeliveryNote(id: number): Observable<SalesDeliveryNote> {
    return this.http.get<SalesDeliveryNote>(`/api/salesdeliverynotes/${id}`);
  }

  createSalesDeliveryNote(note: SalesDeliveryNote): Observable<SalesDeliveryNote> {
    return this.http.post<SalesDeliveryNote>('/api/salesdeliverynotes', note);
  }

  createSalesDeliveryNoteFromOrder(salesOrderId: number): Observable<SalesDeliveryNote> {
    return this.http.post<SalesDeliveryNote>(`/api/salesdeliverynotes/from-order/${salesOrderId}`, {});
  }

  updateSalesDeliveryNote(id: number, note: SalesDeliveryNote): Observable<SalesDeliveryNote> {
    return this.http.put<SalesDeliveryNote>(`/api/salesdeliverynotes/${id}`, note);
  }

  deleteSalesDeliveryNote(id: number): Observable<void> {
    return this.http.delete<void>(`/api/salesdeliverynotes/${id}`);
  }

  convertDeliveryNoteToInvoice(id: number): Observable<SalesInvoice> {
    return this.http.post<SalesInvoice>(`/api/salesdeliverynotes/${id}/convert-to-invoice`, {});
  }

  validateSalesDeliveryNote(id: number): Observable<SalesDeliveryNote> {
    return this.http.post<SalesDeliveryNote>(`/api/salesdeliverynotes/${id}/validate`, {});
  }

  cancelSalesDeliveryNote(id: number, reason?: string): Observable<SalesDeliveryNote> {
    return this.http.post<SalesDeliveryNote>(`/api/salesdeliverynotes/${id}/cancel`, { reason });
  }

  downloadSalesDeliveryNotePdf(id: number): Observable<Blob> {
    return this.http.get(`/api/business-documents/sales-delivery-notes/${id}/pdf`, { responseType: 'blob' });
  }

  // PDF exports
  downloadQuotePdf(id: number): Observable<Blob> {
    return this.http.get(`/api/business-documents/quotes/${id}/pdf`, { responseType: 'blob' });
  }

  downloadSalesOrderPdf(id: number): Observable<Blob> {
    return this.http.get(`/api/business-documents/sales-orders/${id}/pdf`, { responseType: 'blob' });
  }

  downloadSalesInvoicePdf(id: number): Observable<Blob> {
    return this.http.get(`/api/business-documents/sales-invoices/${id}/pdf`, { responseType: 'blob' });
  }

  downloadCreditNotePdf(id: number): Observable<Blob> {
    return this.http.get(`/api/business-documents/credit-notes/${id}/pdf`, { responseType: 'blob' });
  }

  downloadPurchaseOrderPdf(id: number): Observable<Blob> {
    return this.http.get(`/api/business-documents/purchase-orders/${id}/pdf`, { responseType: 'blob' });
  }

  downloadSupplierInvoicePdf(id: number): Observable<Blob> {
    return this.http.get(`/api/business-documents/supplier-invoices/${id}/pdf`, { responseType: 'blob' });
  }

  // Quotes — duplicate
  duplicateQuote(id: number): Observable<Quote> {
    return this.http.post<Quote>(`/api/quotes/${id}/duplicate`, {});
  }

  // Sales Returns (BRC)
  getSalesReturns(search?: string, customerId?: number, salesDeliveryNoteId?: number): Observable<SalesReturn[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    if (customerId) params = params.set('customerId', customerId);
    if (salesDeliveryNoteId) params = params.set('salesDeliveryNoteId', salesDeliveryNoteId);
    return this.http.get<SalesReturn[]>('/api/salesreturns', { params });
  }

  getSalesReturn(id: number): Observable<SalesReturn> {
    return this.http.get<SalesReturn>(`/api/salesreturns/${id}`);
  }

  createSalesReturnFromDelivery(payload: {
    salesDeliveryNoteId: number;
    lines?: { productKey?: string; quantity: number; qualityStatus?: string }[];
    notes?: string;
  }): Observable<SalesReturn> {
    return this.http.post<SalesReturn>('/api/salesreturns/from-delivery', payload);
  }

  receiveSalesReturn(id: number): Observable<SalesReturn> {
    return this.http.post<SalesReturn>(`/api/salesreturns/${id}/receive`, {});
  }

  controlSalesReturn(id: number, qualityStatus?: string): Observable<SalesReturn> {
    return this.http.post<SalesReturn>(`/api/salesreturns/${id}/control`, { qualityStatus });
  }

  integrateSalesReturn(id: number, createCreditNote = false, salesInvoiceId?: number): Observable<SalesReturn> {
    return this.http.post<SalesReturn>(`/api/salesreturns/${id}/integrate`, { createCreditNote, salesInvoiceId });
  }

  cancelSalesReturn(id: number, reason?: string): Observable<SalesReturn> {
    return this.http.post<SalesReturn>(`/api/salesreturns/${id}/cancel`, { reason });
  }

  deleteSalesReturn(id: number): Observable<void> {
    return this.http.delete<void>(`/api/salesreturns/${id}`);
  }

  createCreditNoteFromReturn(id: number, salesInvoiceId?: number): Observable<CreditNote> {
    return this.http.post<CreditNote>(`/api/salesreturns/${id}/create-credit-note`, { salesInvoiceId });
  }

  // Proformas (PF)
  getProformas(search?: string, customerId?: number): Observable<Proforma[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    if (customerId) params = params.set('customerId', customerId);
    return this.http.get<Proforma[]>('/api/proformas', { params });
  }

  getProforma(id: number): Observable<Proforma> {
    return this.http.get<Proforma>(`/api/proformas/${id}`);
  }

  createProforma(payload: {
    quoteId?: number;
    salesOrderId?: number;
    customerId: number;
    notes?: string;
    lines?: { productKey?: string; description?: string; quantity: number; unitPrice: number; vatRate: number }[];
  }): Observable<Proforma> {
    return this.http.post<Proforma>('/api/proformas', payload);
  }

  sendProforma(id: number): Observable<Proforma> {
    return this.http.post<Proforma>(`/api/proformas/${id}/send`, {});
  }

  cancelProforma(id: number, reason?: string): Observable<Proforma> {
    return this.http.post<Proforma>(`/api/proformas/${id}/cancel`, { reason });
  }

  deleteProforma(id: number): Observable<void> {
    return this.http.delete<void>(`/api/proformas/${id}`);
  }

  // Deposit Invoices (AAC)
  getDepositInvoices(search?: string, customerId?: number, salesOrderId?: number): Observable<DepositInvoice[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    if (customerId) params = params.set('customerId', customerId);
    if (salesOrderId) params = params.set('salesOrderId', salesOrderId);
    return this.http.get<DepositInvoice[]>('/api/depositinvoices', { params });
  }

  getDepositInvoice(id: number): Observable<DepositInvoice> {
    return this.http.get<DepositInvoice>(`/api/depositinvoices/${id}`);
  }

  createDepositInvoice(deposit: DepositInvoice): Observable<DepositInvoice> {
    return this.http.post<DepositInvoice>('/api/depositinvoices', deposit);
  }

  validateDepositInvoice(id: number): Observable<DepositInvoice> {
    return this.http.post<DepositInvoice>(`/api/depositinvoices/${id}/validate`, {});
  }

  applyDepositToInvoice(id: number, salesInvoiceId: number): Observable<DepositInvoice> {
    return this.http.post<DepositInvoice>(`/api/depositinvoices/${id}/apply-to-invoice`, { salesInvoiceId });
  }

  cancelDepositInvoice(id: number, reason?: string): Observable<DepositInvoice> {
    return this.http.post<DepositInvoice>(`/api/depositinvoices/${id}/cancel`, { reason });
  }

  // Supplier RFQs (DPF)
  getSupplierRfqs(search?: string, supplierId?: number): Observable<SupplierRfq[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    if (supplierId) params = params.set('supplierId', supplierId);
    return this.http.get<SupplierRfq[]>('/api/supplierrfqs', { params });
  }

  getSupplierRfq(id: number): Observable<SupplierRfq> {
    return this.http.get<SupplierRfq>(`/api/supplierrfqs/${id}`);
  }

  createSupplierRfq(rfq: SupplierRfq): Observable<SupplierRfq> {
    return this.http.post<SupplierRfq>('/api/supplierrfqs', rfq);
  }

  sendSupplierRfq(id: number): Observable<SupplierRfq> {
    return this.http.post<SupplierRfq>(`/api/supplierrfqs/${id}/send`, {});
  }

  convertRfqToPurchaseOrder(id: number, supplierId?: number): Observable<PurchaseOrder> {
    return this.http.post<PurchaseOrder>(`/api/supplierrfqs/${id}/convert-to-purchase-order`, { supplierId });
  }

  cancelSupplierRfq(id: number, reason?: string): Observable<SupplierRfq> {
    return this.http.post<SupplierRfq>(`/api/supplierrfqs/${id}/cancel`, { reason });
  }

  deleteSupplierRfq(id: number): Observable<void> {
    return this.http.delete<void>(`/api/supplierrfqs/${id}`);
  }

  // Supplier Returns (BRF)
  getSupplierReturns(search?: string, supplierId?: number, purchaseOrderId?: number): Observable<SupplierReturn[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    if (supplierId) params = params.set('supplierId', supplierId);
    if (purchaseOrderId) params = params.set('purchaseOrderId', purchaseOrderId);
    return this.http.get<SupplierReturn[]>('/api/supplierreturns', { params });
  }

  getSupplierReturn(id: number): Observable<SupplierReturn> {
    return this.http.get<SupplierReturn>(`/api/supplierreturns/${id}`);
  }

  createSupplierReturn(supplierReturn: SupplierReturn): Observable<SupplierReturn> {
    return this.http.post<SupplierReturn>('/api/supplierreturns', supplierReturn);
  }

  shipSupplierReturn(id: number): Observable<SupplierReturn> {
    return this.http.post<SupplierReturn>(`/api/supplierreturns/${id}/ship`, {});
  }

  cancelSupplierReturn(id: number, reason?: string): Observable<SupplierReturn> {
    return this.http.post<SupplierReturn>(`/api/supplierreturns/${id}/cancel`, { reason });
  }

  createCreditNoteFromSupplierReturn(id: number, supplierInvoiceId?: number): Observable<SupplierCreditNote> {
    return this.http.post<SupplierCreditNote>(`/api/supplierreturns/${id}/create-credit-note`, { supplierInvoiceId });
  }

  // Supplier Credit Notes (AF)
  getSupplierCreditNotes(search?: string, supplierId?: number, supplierInvoiceId?: number): Observable<SupplierCreditNote[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    if (supplierId) params = params.set('supplierId', supplierId);
    if (supplierInvoiceId) params = params.set('supplierInvoiceId', supplierInvoiceId);
    return this.http.get<SupplierCreditNote[]>('/api/suppliercreditnotes', { params });
  }

  getSupplierCreditNote(id: number): Observable<SupplierCreditNote> {
    return this.http.get<SupplierCreditNote>(`/api/suppliercreditnotes/${id}`);
  }

  createSupplierCreditNote(creditNote: SupplierCreditNote): Observable<SupplierCreditNote> {
    return this.http.post<SupplierCreditNote>('/api/suppliercreditnotes', creditNote);
  }

  validateSupplierCreditNote(id: number): Observable<SupplierCreditNote> {
    return this.http.post<SupplierCreditNote>(`/api/suppliercreditnotes/${id}/validate`, {});
  }

  applySupplierCreditNote(id: number): Observable<SupplierCreditNote> {
    return this.http.post<SupplierCreditNote>(`/api/suppliercreditnotes/${id}/apply`, {});
  }

  cancelSupplierCreditNote(id: number, reason?: string): Observable<SupplierCreditNote> {
    return this.http.post<SupplierCreditNote>(`/api/suppliercreditnotes/${id}/cancel`, { reason });
  }

  // Numbering sequences
  getNumberingSequences(): Observable<DocumentNumberSequence[]> {
    return this.http.get<DocumentNumberSequence[]>('/api/numberingsequences');
  }

  ensureNumberingDefaults(): Observable<DocumentNumberSequence[]> {
    return this.http.post<DocumentNumberSequence[]>('/api/numberingsequences/ensure-defaults', {});
  }

  previewNextNumber(documentType: string): Observable<{ number: string }> {
    return this.http.get<{ number: string }>('/api/numberingsequences/preview', {
      params: { documentType }
    });
  }

  updateNumberingSequence(id: number, sequence: DocumentNumberSequence): Observable<DocumentNumberSequence> {
    return this.http.put<DocumentNumberSequence>(`/api/numberingsequences/${id}`, sequence);
  }
}
