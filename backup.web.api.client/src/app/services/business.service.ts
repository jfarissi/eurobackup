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
  ReceiveDeliveryResult,
  Receipt,
  ComptabiliserResult,
  ComptabiliserInvoiceResult,
  SupplierInvoice,
  SupplierInvoicePurchaseOrderMatchResult,
  StockMovement,
  CashSession,
  CashOperation,
  DocumentNumberSequence
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

  // Sales Orders
  getSalesOrders(search?: string): Observable<SalesOrder[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    return this.http.get<SalesOrder[]>('/api/salesorders', { params });
  }

  getSalesOrder(id: number): Observable<SalesOrder> {
    return this.http.get<SalesOrder>(`/api/salesorders/${id}`);
  }

  createSalesOrder(order: SalesOrder): Observable<SalesOrder> {
    return this.http.post<SalesOrder>('/api/salesorders', order);
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

  recordPayment(invoiceId: number, amount: number, paymentMethod: string, notes?: string): Observable<SalesInvoice> {
    return this.http.post<SalesInvoice>(`/api/salesinvoices/${invoiceId}/pay`, { amount, paymentMethod, notes });
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

  createCreditNoteFromInvoice(salesInvoiceId: number, notes?: string): Observable<CreditNote> {
    return this.http.post<CreditNote>('/api/creditnotes/from-invoice', { salesInvoiceId, notes });
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
  getActiveCashSession(): Observable<CashSession> {
    return this.http.get<CashSession>('/api/cash/active-session');
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
