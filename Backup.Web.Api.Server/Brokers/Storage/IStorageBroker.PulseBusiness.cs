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
        ValueTask<Quote?> SelectQuoteByIdAsync(int id);
        ValueTask<Quote> UpdateQuoteAsync(Quote quote);

        // Sales Orders
        ValueTask<SalesOrder> InsertSalesOrderAsync(SalesOrder salesOrder);
        IQueryable<SalesOrder> SelectAllSalesOrders();
        ValueTask<SalesOrder?> SelectSalesOrderByIdAsync(int id);
        ValueTask<SalesOrder> UpdateSalesOrderAsync(SalesOrder salesOrder);

        // Sales Invoices
        ValueTask<SalesInvoice> InsertSalesInvoiceAsync(SalesInvoice salesInvoice);
        IQueryable<SalesInvoice> SelectAllSalesInvoices();
        ValueTask<SalesInvoice?> SelectSalesInvoiceByIdAsync(int id);
        ValueTask<SalesInvoice> UpdateSalesInvoiceAsync(SalesInvoice salesInvoice);

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

        // Receipts (ErpReceipts / ErpReceiptLines)
        ValueTask<Receipt> InsertReceiptAsync(Receipt receipt);
        IQueryable<Receipt> SelectAllReceipts();
        ValueTask<Receipt?> SelectReceiptByIdAsync(int id);
        ValueTask<Receipt?> SelectReceiptByDocumentIdAsync(int documentId);

        // Sales Delivery Notes (BL vente)
        ValueTask<SalesDeliveryNote> InsertSalesDeliveryNoteAsync(SalesDeliveryNote note);
        IQueryable<SalesDeliveryNote> SelectAllSalesDeliveryNotes();
        ValueTask<SalesDeliveryNote?> SelectSalesDeliveryNoteByIdAsync(int id);
        ValueTask<SalesDeliveryNote> UpdateSalesDeliveryNoteAsync(SalesDeliveryNote note);
        ValueTask DeleteSalesDeliveryNoteAsync(SalesDeliveryNote note);
    }
}
