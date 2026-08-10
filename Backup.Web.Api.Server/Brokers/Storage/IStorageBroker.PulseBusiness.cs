using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Entities;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial interface IStorageBroker
    {
        // Customers
        ValueTask<Customer> InsertCustomerAsync(Customer customer);
        IQueryable<Customer> SelectAllCustomers();
        ValueTask<Customer?> SelectCustomerByIdAsync(int id);
        ValueTask<Customer> UpdateCustomerAsync(Customer customer);
        ValueTask DeleteCustomerAsync(Customer customer);

        // Suppliers
        ValueTask<Supplier> InsertSupplierAsync(Supplier supplier);
        IQueryable<Supplier> SelectAllSuppliers();
        ValueTask<Supplier?> SelectSupplierByIdAsync(int id);
        ValueTask<Supplier> UpdateSupplierAsync(Supplier supplier);
        ValueTask DeleteSupplierAsync(Supplier supplier);

        // Quotes
        ValueTask<Quote> InsertQuoteAsync(Quote quote);
        IQueryable<Quote> SelectAllQuotes();
        IQueryable<Quote> SelectDeletedQuotes();
        ValueTask<Quote?> SelectQuoteByIdAsync(int id);
        ValueTask<Quote?> SelectQuoteByIdIncludingDeletedAsync(int id);
        ValueTask<Quote> UpdateQuoteAsync(Quote quote);
        ValueTask DeleteQuoteAsync(Quote quote);
        ValueTask PurgeQuoteAsync(Quote quote);

        // Sales Orders
        ValueTask<SalesOrder> InsertSalesOrderAsync(SalesOrder salesOrder);
        IQueryable<SalesOrder> SelectAllSalesOrders();
        IQueryable<SalesOrder> SelectDeletedSalesOrders();
        ValueTask<SalesOrder?> SelectSalesOrderByIdAsync(int id);
        ValueTask<SalesOrder?> SelectSalesOrderByIdIncludingDeletedAsync(int id);
        ValueTask<SalesOrder> UpdateSalesOrderAsync(SalesOrder salesOrder);
        ValueTask DeleteSalesOrderAsync(SalesOrder salesOrder);
        ValueTask PurgeSalesOrderAsync(SalesOrder salesOrder);

        // Sales Invoices
        ValueTask<SalesInvoice> InsertSalesInvoiceAsync(SalesInvoice salesInvoice);
        IQueryable<SalesInvoice> SelectAllSalesInvoices();
        IQueryable<SalesInvoice> SelectDeletedSalesInvoices();
        ValueTask<SalesInvoice?> SelectSalesInvoiceByIdAsync(int id);
        ValueTask<SalesInvoice?> SelectSalesInvoiceByIdIncludingDeletedAsync(int id);
        ValueTask<SalesInvoice> UpdateSalesInvoiceAsync(SalesInvoice salesInvoice);
        ValueTask DeleteSalesInvoiceAsync(SalesInvoice salesInvoice);
        ValueTask PurgeSalesInvoiceAsync(SalesInvoice salesInvoice);

        // Credit Notes
        ValueTask<CreditNoteEntity> InsertCreditNoteAsync(CreditNoteEntity creditNote);
        IQueryable<CreditNoteEntity> SelectAllCreditNotes();
        ValueTask<CreditNoteEntity?> SelectCreditNoteByIdAsync(int id);
        ValueTask<CreditNoteEntity> UpdateCreditNoteAsync(CreditNoteEntity creditNote);

        // Purchase Orders
        ValueTask<PurchaseOrder> InsertPurchaseOrderAsync(PurchaseOrder purchaseOrder);
        IQueryable<PurchaseOrder> SelectAllPurchaseOrders();
        ValueTask<PurchaseOrder?> SelectPurchaseOrderByIdAsync(int id);
        ValueTask<PurchaseOrder> UpdatePurchaseOrderAsync(PurchaseOrder purchaseOrder);

        // Supplier Invoices
        ValueTask<SupplierInvoiceEntity> InsertSupplierInvoiceAsync(SupplierInvoiceEntity supplierInvoice);
        IQueryable<SupplierInvoiceEntity> SelectAllSupplierInvoices();
        ValueTask<SupplierInvoiceEntity?> SelectSupplierInvoiceByIdAsync(int id);
        ValueTask<SupplierInvoiceEntity> UpdateSupplierInvoiceAsync(SupplierInvoiceEntity supplierInvoice);

        // Supplier Payments (règlements factures achat)
        ValueTask<SupplierPayment> InsertSupplierPaymentAsync(SupplierPayment payment);
        IQueryable<SupplierPayment> SelectAllSupplierPayments();
        ValueTask<SupplierPayment?> SelectSupplierPaymentByIdAsync(int id);
        ValueTask<SupplierPayment> UpdateSupplierPaymentAsync(SupplierPayment payment);

        // Stock Movements
        ValueTask<StockMovement> InsertStockMovementAsync(StockMovement movement);
        IQueryable<StockMovement> SelectAllStockMovements();
        ValueTask<Backup.Web.Api.Server.Models.StockItem> InsertStockAsync(Backup.Web.Api.Server.Models.StockItem stockItem);
        ValueTask<Backup.Web.Api.Server.Models.StockItem> UpdateStockAsync(Backup.Web.Api.Server.Models.StockItem stockItem);

        // Document Number Sequences
        ValueTask<DocumentNumberSequence> InsertNumberSequenceAsync(DocumentNumberSequence sequence);
        IQueryable<DocumentNumberSequence> SelectAllNumberSequences();
        ValueTask<DocumentNumberSequence?> SelectNumberSequenceByTypeAsync(string documentType, string? companyId);
        ValueTask<DocumentNumberSequence> UpdateNumberSequenceAsync(DocumentNumberSequence sequence);

        // Cash Sessions & Operations
        ValueTask<CashSession> InsertCashSessionAsync(CashSession session);
        IQueryable<CashSession> SelectAllCashSessions();
        ValueTask<CashSession?> SelectCashSessionByIdAsync(int id);
        ValueTask<CashSession?> SelectActiveCashSessionAsync(string? companyId);
        ValueTask<CashSession> UpdateCashSessionAsync(CashSession session);
        ValueTask<CashOperation> InsertCashOperationAsync(CashOperation operation);
        IQueryable<CashOperation> SelectCashOperationsBySessionId(int sessionId);

        // Payments (règlements factures vente)
        ValueTask<Payment> InsertPaymentAsync(Payment payment);
        IQueryable<Payment> SelectAllPayments();
        ValueTask<Payment?> SelectPaymentByIdAsync(int id);
        ValueTask<Payment?> SelectPaymentByIdForUpdateAsync(int id);
        ValueTask<Payment> UpdatePaymentAsync(Payment payment);
        IQueryable<Payment> SelectPaymentsBySalesInvoiceId(int salesInvoiceId);

        // Receipts (ErpReceipts / ErpReceiptLines)
        ValueTask<Receipt> InsertReceiptAsync(Receipt receipt);
        IQueryable<Receipt> SelectAllReceipts();
        ValueTask<Receipt?> SelectReceiptByIdAsync(int id);
        ValueTask<Receipt?> SelectReceiptByDocumentIdAsync(int documentId);
        ValueTask<Receipt> UpdateReceiptAsync(Receipt receipt);

        // Sales Delivery Notes (BL vente)
        ValueTask<SalesDeliveryNote> InsertSalesDeliveryNoteAsync(SalesDeliveryNote note);
        IQueryable<SalesDeliveryNote> SelectAllSalesDeliveryNotes();
        IQueryable<SalesDeliveryNote> SelectDeletedSalesDeliveryNotes();
        ValueTask<SalesDeliveryNote?> SelectSalesDeliveryNoteByIdAsync(int id);
        ValueTask<SalesDeliveryNote?> SelectSalesDeliveryNoteByIdIncludingDeletedAsync(int id);
        ValueTask<SalesDeliveryNote> UpdateSalesDeliveryNoteAsync(SalesDeliveryNote note);
        ValueTask DeleteSalesDeliveryNoteAsync(SalesDeliveryNote note);
        ValueTask PurgeSalesDeliveryNoteAsync(SalesDeliveryNote note);

        // Accounting entries
        ValueTask<AccountingEntry> InsertAccountingEntryAsync(AccountingEntry entry);
        IQueryable<AccountingEntry> SelectAllAccountingEntries();
        ValueTask<AccountingEntry?> SelectAccountingEntryByIdAsync(int id);

        // Document audit (P3)
        ValueTask<DocumentAuditLog> InsertDocumentAuditLogAsync(DocumentAuditLog log);
        IQueryable<DocumentAuditLog> SelectAllDocumentAuditLogs();
        IQueryable<EntityAuditLog> SelectAllEntityAuditLogs();

        // Sales Returns (BRC vente — RG-BR1–5)
        ValueTask<SalesReturn> InsertSalesReturnAsync(SalesReturn salesReturn);
        IQueryable<SalesReturn> SelectAllSalesReturns();
        ValueTask<SalesReturn?> SelectSalesReturnByIdAsync(int id);
        ValueTask<SalesReturn> UpdateSalesReturnAsync(SalesReturn salesReturn);
        ValueTask DeleteSalesReturnAsync(SalesReturn salesReturn);

        // Supplier Credit Notes (AF achat — RG-AF1–5)
        ValueTask<SupplierCreditNoteEntity> InsertSupplierCreditNoteAsync(SupplierCreditNoteEntity creditNote);
        IQueryable<SupplierCreditNoteEntity> SelectAllSupplierCreditNotes();
        ValueTask<SupplierCreditNoteEntity?> SelectSupplierCreditNoteByIdAsync(int id);
        ValueTask<SupplierCreditNoteEntity> UpdateSupplierCreditNoteAsync(SupplierCreditNoteEntity creditNote);

        // Proformas (PF vente — RG-PF1–4)
        ValueTask<Proforma> InsertProformaAsync(Proforma proforma);
        IQueryable<Proforma> SelectAllProformas();
        ValueTask<Proforma?> SelectProformaByIdAsync(int id);
        ValueTask<Proforma> UpdateProformaAsync(Proforma proforma);
        ValueTask DeleteProformaAsync(Proforma proforma);

        // Deposit Invoices / Acomptes (AAC vente — RG-AA1–4)
        ValueTask<DepositInvoice> InsertDepositInvoiceAsync(DepositInvoice deposit);
        IQueryable<DepositInvoice> SelectAllDepositInvoices();
        ValueTask<DepositInvoice?> SelectDepositInvoiceByIdAsync(int id);
        ValueTask<DepositInvoice> UpdateDepositInvoiceAsync(DepositInvoice deposit);

        // Supplier RFQ (DPF achat — RG-DPF1–4)
        ValueTask<SupplierRfq> InsertSupplierRfqAsync(SupplierRfq rfq);
        IQueryable<SupplierRfq> SelectAllSupplierRfqs();
        ValueTask<SupplierRfq?> SelectSupplierRfqByIdAsync(int id);
        ValueTask<SupplierRfq> UpdateSupplierRfqAsync(SupplierRfq rfq);
        ValueTask DeleteSupplierRfqAsync(SupplierRfq rfq);

        // Supplier Returns (BRF achat — RG-BRF1–5)
        ValueTask<SupplierReturn> InsertSupplierReturnAsync(SupplierReturn supplierReturn);
        IQueryable<SupplierReturn> SelectAllSupplierReturns();
        ValueTask<SupplierReturn?> SelectSupplierReturnByIdAsync(int id);
        ValueTask<SupplierReturn> UpdateSupplierReturnAsync(SupplierReturn supplierReturn);
        ValueTask DeleteSupplierReturnAsync(SupplierReturn supplierReturn);

        // Payment Allocations (RG-RG2 lite)
        ValueTask<PaymentAllocation> InsertPaymentAllocationAsync(PaymentAllocation allocation);
        IQueryable<PaymentAllocation> SelectAllPaymentAllocations();

        // Lettering (RG-LT1–4 lite)
        ValueTask<LetteringGroup> InsertLetteringGroupAsync(LetteringGroup group);
        IQueryable<LetteringGroup> SelectAllLetteringGroups();
        ValueTask<LetteringGroup?> SelectLetteringGroupByIdAsync(int id);
        ValueTask<LetteringGroup> UpdateLetteringGroupAsync(LetteringGroup group);

        // Customer Price List (RG-PT1–5 lite)
        ValueTask<CustomerPriceListItem> InsertCustomerPriceListItemAsync(CustomerPriceListItem item);
        IQueryable<CustomerPriceListItem> SelectAllCustomerPriceListItems();
        ValueTask<CustomerPriceListItem?> SelectCustomerPriceListItemByIdAsync(int id);
        ValueTask<CustomerPriceListItem> UpdateCustomerPriceListItemAsync(CustomerPriceListItem item);
        ValueTask DeleteCustomerPriceListItemAsync(CustomerPriceListItem item);
    }
}
