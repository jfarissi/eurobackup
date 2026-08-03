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
  /** Plafond d'encours TTC (0 = illimité). */
  creditLimit?: number;
  /** Conditions de paiement (ex: "30 jours", "60D EOM"). */
  paymentTerms?: string;
  /** Active | Blocked | Closed */
  status?: string;
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
  balance?: number;
  paymentTerms?: string;
  isActive?: boolean;
  /** Active | Blocked | Closed */
  status?: string;
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
  invoicedQuantity?: number;
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
  billingAddress?: string;
  shippingAddress?: string;
  companyId?: string;
  createdAt?: string;
  lines: SalesOrderLine[];
}

export interface SalesBackorderLine {
  orderId: number;
  orderNumber: string;
  orderStatus: string;
  customerId: number;
  customerName: string;
  productKey: string;
  description: string;
  orderedQuantity: number;
  deliveredQuantity: number;
  remainingQuantity: number;
  stockOnHand: number;
  isStockout: boolean;
}

export interface SalesPilotage {
  pendingCount: number;
  backorderLineCount: number;
  stockoutLineCount: number;
  pendingOrders: SalesOrder[];
  backorderLines: SalesBackorderLine[];
  stockoutLines: SalesBackorderLine[];
}

export interface DocumentAuditLog {
  id?: number;
  documentType: string;
  documentId: number;
  action: string;
  summary?: string;
  details?: string;
  actor?: string;
  companyId?: string;
  createdAt: string;
}

export interface SalesTrashItem {
  documentType: 'Invoice' | 'Order' | 'DeliveryNote' | 'Quote' | string;
  id: number;
  number: string;
  customerName?: string;
  customerId: number;
  status: string;
  totalTTC: number;
  deletedAt?: string;
  deletedBy?: string;
  canRestore: boolean;
  canPurge?: boolean;
  restoreBlockedReason?: string;
}

export interface SalesInvoiceLine {
  id?: number;
  salesInvoiceId?: number;
  productKey: string;
  description: string;
  quantity: number;
  orderedQuantity?: number;
  deliveredQuantity?: number;
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
  /** BL source at create-time only (API links via SalesDeliveryNote.salesInvoiceId). */
  salesDeliveryNoteId?: number;
  /** RG-BL7 : plusieurs BL → une facture. */
  salesDeliveryNoteIds?: number[];
  date: string;
  dueDate: string;
  status: string;
  totalHT: number;
  totalVat: number;
  totalTTC: number;
  paidAmount: number;
  creditedAmount?: number;
  remainingAmount?: number;
  /** Linked to a delivered/invoiced BL — required for payment. */
  hasDeliveredSource?: boolean;
  /** Computed by API: overdue unpaid invoice. */
  isOverdue?: boolean;
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
  salesReturnId?: number;
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
  receivedQuantityMismatchCount?: number;
  priceMismatchCount: number;
  isBalanced: boolean;
  requiresApproval?: boolean;
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

/** Règlement client (table Payments, aligné Pulse ErpPayments). */
export interface Payment {
  id?: number;
  companyId?: string;
  salesInvoiceId: number;
  salesInvoice?: SalesInvoice;
  amount: number;
  roundingDifference?: number;
  receivedAmount?: number;
  changeAmount?: number;
  paidAt: string;
  method?: string;
  reference?: string;
  bank?: string;
  status: string;
  cashSessionId?: number | null;
  terminalTransactionId?: string | null;
  createdBy?: string;
  createdAt?: string;
}

export interface RecordPaymentResult {
  invoice: SalesInvoice;
  payment: Payment;
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

export interface AccountingEntryLine {
  id?: number;
  accountingEntryId?: number;
  accountCode: string;
  accountLabel: string;
  debit: number;
  credit: number;
  lineNumber: number;
}

export interface AccountingEntry {
  id?: number;
  entryNumber: string;
  entryDate: string;
  journalType: string;
  referenceType: string;
  referenceId: number;
  description: string;
  status: string;
  companyId?: string;
  createdBy?: string;
  createdAt?: string;
  lines: AccountingEntryLine[];
}

export interface ManualAccountingEntryRequest {
  entryDate?: string;
  journalType?: string;
  description?: string;
  referenceType?: string;
  referenceId?: number;
  lines: AccountingEntryLine[];
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

/** Ligne de bon de retour client (BRC). */
export interface SalesReturnLine {
  id?: number;
  salesReturnId?: number;
  productKey: string;
  description: string;
  quantity: number;
  unitPrice: number;
  vatRate: number;
  totalHT: number;
  totalTTC: number;
  lineNumber: number;
  /** Conforme, Degraded, NonRecoverable */
  qualityStatus?: string;
}

/** Bon de retour client (BRC) — RG-BR1–5. Toujours lié à un BL livré/facturé. */
export interface SalesReturn {
  id?: number;
  returnNumber: string;
  customerId: number;
  customer?: Customer;
  salesDeliveryNoteId: number;
  salesDeliveryNote?: SalesDeliveryNote;
  salesOrderId?: number;
  returnDate: string;
  /** Draft, Received, Controlled, Integrated, Cancelled */
  status: string;
  qualityStatus?: string;
  totalHT: number;
  totalVat: number;
  totalTTC: number;
  currencyCode?: string;
  notes?: string;
  companyId?: string;
  createdAt?: string;
  stockApplied?: boolean;
  creditNoteId?: number;
  lines: SalesReturnLine[];
}

export interface ProformaLine {
  id?: number;
  proformaId?: number;
  productKey: string;
  description: string;
  quantity: number;
  unitPrice: number;
  vatRate: number;
  totalHT: number;
  totalTTC: number;
  lineNumber: number;
}

/** Facture proforma (PF) — jamais d'effet GL/stock, jamais convertible directement en facture. */
export interface Proforma {
  id?: number;
  proformaNumber: string;
  customerId: number;
  customer?: Customer;
  quoteId?: number;
  salesOrderId?: number;
  date: string;
  /** Draft, Sent, Cancelled */
  status: string;
  totalHT: number;
  totalVat: number;
  totalTTC: number;
  currencyCode?: string;
  notes?: string;
  companyId?: string;
  createdAt?: string;
  lines: ProformaLine[];
}

/** Facture d'acompte (AAC) — toujours liée à une commande. */
export interface DepositInvoice {
  id?: number;
  depositNumber: string;
  customerId: number;
  customer?: Customer;
  salesOrderId: number;
  salesOrder?: SalesOrder;
  date: string;
  amountHT: number;
  vatRate: number;
  amountTTC: number;
  /** Draft, Validated, Applied, Cancelled */
  status: string;
  currencyCode?: string;
  notes?: string;
  companyId?: string;
  createdAt?: string;
  appliedSalesInvoiceId?: number;
  appliedAt?: string;
}

export interface SupplierRfqLine {
  id?: number;
  supplierRfqId?: number;
  productKey: string;
  description: string;
  quantity: number;
  estimatedUnitPrice: number;
  lineNumber: number;
}

/** Demande de prix fournisseur (DPF). */
export interface SupplierRfq {
  id?: number;
  rfqNumber: string;
  supplierId?: number;
  supplier?: Supplier;
  date: string;
  /** Draft, Sent, Awaiting, Processed, Cancelled */
  status: string;
  notes?: string;
  companyId?: string;
  createdAt?: string;
  purchaseOrderId?: number;
  lines: SupplierRfqLine[];
}

export interface SupplierReturnLine {
  id?: number;
  supplierReturnId?: number;
  productKey: string;
  description: string;
  quantity: number;
  unitPrice: number;
  vatRate: number;
  totalHT: number;
  totalTTC: number;
  lineNumber: number;
  /** Conforme, Degraded, NonConforming */
  qualityStatus?: string;
}

/** Bon de retour fournisseur (BRF). */
export interface SupplierReturn {
  id?: number;
  returnNumber: string;
  supplierId: number;
  supplier?: Supplier;
  purchaseOrderId?: number;
  receiptId?: number;
  supplierInvoiceId?: number;
  date: string;
  /** Draft, Shipped, Cancelled */
  status: string;
  totalHT: number;
  totalVat: number;
  totalTTC: number;
  currencyCode?: string;
  notes?: string;
  companyId?: string;
  createdAt?: string;
  stockApplied?: boolean;
  creditNoteId?: number;
  lines: SupplierReturnLine[];
}

export interface SupplierCreditNoteLine {
  id?: number;
  supplierCreditNoteEntityId?: number;
  productKey: string;
  description: string;
  quantity: number;
  unitPrice: number;
  vatRate: number;
  totalHT: number;
  totalTTC: number;
  lineNumber: number;
}

/** Avoir fournisseur (AF) — toujours lié à une facture fournisseur. */
export interface SupplierCreditNote {
  id?: number;
  creditNoteNumber: string;
  supplierId: number;
  supplier?: Supplier;
  supplierInvoiceId: number;
  supplierInvoice?: SupplierInvoice;
  date: string;
  /** Draft, Validated, Applied, Cancelled */
  status: string;
  totalHT: number;
  totalVat: number;
  totalTTC: number;
  notes?: string;
  companyId?: string;
  createdAt?: string;
  lines: SupplierCreditNoteLine[];
}
