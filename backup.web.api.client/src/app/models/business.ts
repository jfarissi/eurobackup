export interface Customer {
  id?: number;
  customerCode: string;
  name: string;
  vatNumber?: string;
  address?: string;
  city?: string;
  postalCode?: string;
  country?: string;
  email?: string;
  phone?: string;
  balance: number;
  companyId?: string;
  createdAt?: string;
  updatedAt?: string;
}

export interface Supplier {
  id?: number;
  supplierCode: string;
  name: string;
  vatNumber?: string;
  address?: string;
  city?: string;
  postalCode?: string;
  country?: string;
  email?: string;
  phone?: string;
  companyId?: string;
  createdAt?: string;
  updatedAt?: string;
}

export interface QuoteLine {
  id?: number;
  quoteId?: number;
  productKey: string;
  description: string;
  quantity: number;
  unitPrice: number;
  vatRate: number;
  totalHT: number;
  totalTTC: number;
  lineNumber: number;
}

export interface Quote {
  id?: number;
  quoteNumber: string;
  customerId: number;
  customer?: Customer;
  date: string;
  expirationDate: string;
  status: string;
  totalHT: number;
  totalVat: number;
  totalTTC: number;
  notes?: string;
  companyId?: string;
  createdAt?: string;
  lines: QuoteLine[];
}

export interface SalesOrderLine {
  id?: number;
  salesOrderId?: number;
  productKey: string;
  description: string;
  quantity: number;
  deliveredQuantity: number;
  unitPrice: number;
  vatRate: number;
  totalHT: number;
  totalTTC: number;
  lineNumber: number;
}

export interface SalesOrder {
  id?: number;
  orderNumber: string;
  customerId: number;
  customer?: Customer;
  quoteId?: number;
  date: string;
  status: string;
  totalHT: number;
  totalVat: number;
  totalTTC: number;
  notes?: string;
  companyId?: string;
  createdAt?: string;
  lines: SalesOrderLine[];
}

export interface SalesInvoiceLine {
  id?: number;
  salesInvoiceId?: number;
  productKey: string;
  description: string;
  quantity: number;
  unitPrice: number;
  vatRate: number;
  totalHT: number;
  totalTTC: number;
  lineNumber: number;
}

export interface SalesInvoice {
  id?: number;
  invoiceNumber: string;
  customerId: number;
  customer?: Customer;
  salesOrderId?: number;
  date: string;
  dueDate: string;
  status: string;
  totalHT: number;
  totalVat: number;
  totalTTC: number;
  paidAmount: number;
  notes?: string;
  companyId?: string;
  createdAt?: string;
  lines: SalesInvoiceLine[];
}

export interface CreditNoteLine {
  id?: number;
  creditNoteEntityId?: number;
  productKey: string;
  description: string;
  quantity: number;
  unitPrice: number;
  vatRate: number;
  totalHT: number;
  totalTTC: number;
  lineNumber: number;
}

export interface CreditNote {
  id?: number;
  creditNoteNumber: string;
  customerId: number;
  customer?: Customer;
  salesInvoiceId?: number;
  date: string;
  status: string;
  totalHT: number;
  totalVat: number;
  totalTTC: number;
  notes?: string;
  companyId?: string;
  createdAt?: string;
  lines: CreditNoteLine[];
}

export interface PurchaseOrderLine {
  id?: number;
  purchaseOrderId?: number;
  productKey: string;
  description: string;
  quantity: number;
  receivedQuantity: number;
  unitPrice: number;
  vatRate: number;
  totalHT: number;
  totalTTC: number;
  lineNumber: number;
}

export interface PurchaseOrder {
  id?: number;
  orderNumber: string;
  supplierId: number;
  supplier?: Supplier;
  date: string;
  expectedDeliveryDate?: string;
  status: string;
  totalHT: number;
  totalVat: number;
  totalTTC: number;
  notes?: string;
  companyId?: string;
  createdAt?: string;
  supplierInvoices?: SupplierInvoice[];
  lines: PurchaseOrderLine[];
}

export interface ReceiveDeliveryResult {
  purchaseOrder: PurchaseOrder;
  stockUpdated: boolean;
  stockAlreadyApplied: boolean;
  stockMovementCount: number;
  stockQuantityIn: number;
  warnings: string[];
}

export interface ReceiptLine {
  id?: number;
  receiptId?: number;
  productKey: string;
  description: string;
  quantityReceived: number;
  unitPriceExclTax: number;
  taxRatePercent: number;
  lineAmountExclTax: number;
  lineTaxAmount: number;
  lineNumber: number;
}

export interface Receipt {
  id?: number;
  receiptNumber: string;
  purchaseOrderId?: number;
  purchaseOrder?: PurchaseOrder;
  supplierId: number;
  supplier?: Supplier;
  documentId?: number;
  receivedAt: string;
  status: string;
  notes?: string;
  companyId?: string;
  createdBy?: string;
  createdAt?: string;
  lines: ReceiptLine[];
}

export interface ComptabiliserResult {
  receipt: Receipt;
  stockUpdated: boolean;
  stockAlreadyApplied: boolean;
  stockMovementCount: number;
  stockQuantityIn: number;
  warnings: string[];
}

export interface ComptabiliserInvoiceResult {
  invoice: SupplierInvoice;
  warnings: string[];
}

export interface SupplierInvoiceLine {
  id?: number;
  supplierInvoiceEntityId?: number;
  productKey: string;
  description: string;
  quantity: number;
  unitPrice: number;
  vatRate: number;
  totalHT: number;
  totalTTC: number;
  lineNumber: number;
}

export interface SupplierInvoice {
  id?: number;
  invoiceNumber: string;
  supplierId: number;
  supplier?: Supplier;
  documentId?: number;
  purchaseOrderId?: number;
  purchaseOrder?: PurchaseOrder;
  date: string;
  dueDate: string;
  status: string;
  totalHT: number;
  totalVat: number;
  totalTTC: number;
  notes?: string;
  companyId?: string;
  createdAt?: string;
  lines: SupplierInvoiceLine[];
}

export interface SupplierInvoicePurchaseOrderMatchResult {
  invoice: SupplierInvoice;
  purchaseOrder: PurchaseOrder;
  invoiceTotalHt: number;
  purchaseOrderTotalHt: number;
  totalHtDelta: number;
  matchedLineCount: number;
  missingInvoiceLineCount: number;
  missingPurchaseOrderLineCount: number;
  quantityMismatchCount: number;
  priceMismatchCount: number;
  isBalanced: boolean;
  warnings: string[];
}

export interface SalesDeliveryNoteLine {
  id?: number;
  salesDeliveryNoteId?: number;
  productKey: string;
  description: string;
  orderedQuantity: number;
  deliveredQuantity: number;
  unitPrice: number;
  vatRate: number;
  totalHT: number;
  totalTTC: number;
  lineNumber: number;
}

export interface SalesDeliveryNote {
  id?: number;
  deliveryNumber: string;
  customerId: number;
  customer?: Customer;
  salesOrderId?: number;
  salesOrder?: SalesOrder;
  salesInvoiceId?: number;
  deliveryDate: string;
  /** Draft, Sent, Delivered, Invoiced, Cancelled */
  status: string;
  deliveryAddress?: string;
  totalHT: number;
  totalVat: number;
  totalTTC: number;
  notes?: string;
  companyId?: string;
  createdAt?: string;
  lines: SalesDeliveryNoteLine[];
}

export interface StockMovement {
  id?: number;
  productKey: string;
  movementType: 'In' | 'Out' | 'Adjustment' | 'Transfer';
  quantity: number;
  reason?: string;
  referenceDocument?: string;
  companyId?: string;
  createdBy?: string;
  createdAt?: string;
}

export interface CashOperation {
  id?: number;
  cashSessionId: number;
  operationType: 'Deposit' | 'Withdrawal' | 'SalePayment';
  amount: number;
  description?: string;
  referenceDocument?: string;
  createdBy?: string;
  createdAt?: string;
}

export interface CashSession {
  id?: number;
  sessionNumber: string;
  openedAt: string;
  closedAt?: string;
  openingBalance: number;
  closingBalance?: number;
  expectedClosingBalance?: number;
  status: 'Open' | 'Closed';
  openedBy?: string;
  closedBy?: string;
  companyId?: string;
  operations?: CashOperation[];
}

export interface DocumentNumberSequence {
  id?: number;
  documentType: string;
  prefix: string;
  year: number;
  nextNumber: number;
  formatPattern: string;
  companyId?: string;
}
