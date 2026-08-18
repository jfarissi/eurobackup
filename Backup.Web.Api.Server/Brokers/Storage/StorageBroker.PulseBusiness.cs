using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial class StorageBroker
    {
        public DbSet<Customer> Customers { get; set; } = null!;
        public DbSet<Supplier> Suppliers { get; set; } = null!;
        public DbSet<Quote> Quotes { get; set; } = null!;
        public DbSet<QuoteLine> QuoteLines { get; set; } = null!;
        public DbSet<SalesOrder> SalesOrders { get; set; } = null!;
        public DbSet<SalesOrderLine> SalesOrderLines { get; set; } = null!;
        public DbSet<SalesInvoice> SalesInvoices { get; set; } = null!;
        public DbSet<SalesInvoiceLine> SalesInvoiceLines { get; set; } = null!;
        public DbSet<CreditNoteEntity> CreditNotes { get; set; } = null!;
        public DbSet<CreditNoteLineEntity> CreditNoteLines { get; set; } = null!;
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; } = null!;
        public DbSet<PurchaseOrderLine> PurchaseOrderLines { get; set; } = null!;
        public DbSet<SupplierInvoiceEntity> SupplierInvoices { get; set; } = null!;
        public DbSet<SupplierInvoiceLineEntity> SupplierInvoiceLines { get; set; } = null!;
        public DbSet<SupplierPayment> SupplierPayments { get; set; } = null!;
        public DbSet<StockMovement> StockMovements { get; set; } = null!;
        public DbSet<DocumentNumberSequence> DocumentNumberSequences { get; set; } = null!;
        public DbSet<CashSession> CashSessions { get; set; } = null!;
        public DbSet<CashOperation> CashOperations { get; set; } = null!;
        public DbSet<Payment> Payments { get; set; } = null!;
        public DbSet<Receipt> Receipts { get; set; } = null!;
        public DbSet<ReceiptLine> ReceiptLines { get; set; } = null!;
        public DbSet<SalesDeliveryNote> SalesDeliveryNotes { get; set; } = null!;
        public DbSet<SalesDeliveryNoteLine> SalesDeliveryNoteLines { get; set; } = null!;
        public DbSet<AccountingEntry> AccountingEntries { get; set; } = null!;
        public DbSet<AccountingEntryLine> AccountingEntryLines { get; set; } = null!;
        public DbSet<DocumentAuditLog> DocumentAuditLogs { get; set; } = null!;
        public DbSet<EntityAuditLog> EntityAuditLogs { get; set; } = null!;
        public DbSet<SalesReturn> SalesReturns { get; set; } = null!;
        public DbSet<SalesReturnLine> SalesReturnLines { get; set; } = null!;
        public DbSet<SupplierCreditNoteEntity> SupplierCreditNotes { get; set; } = null!;
        public DbSet<SupplierCreditNoteLineEntity> SupplierCreditNoteLines { get; set; } = null!;
        public DbSet<Proforma> Proformas { get; set; } = null!;
        public DbSet<ProformaLine> ProformaLines { get; set; } = null!;
        public DbSet<DepositInvoice> DepositInvoices { get; set; } = null!;
        public DbSet<SupplierRfq> SupplierRfqs { get; set; } = null!;
        public DbSet<SupplierRfqLine> SupplierRfqLines { get; set; } = null!;
        public DbSet<SupplierReturn> SupplierReturns { get; set; } = null!;
        public DbSet<SupplierReturnLine> SupplierReturnLines { get; set; } = null!;
        public DbSet<PaymentAllocation> PaymentAllocations { get; set; } = null!;
        public DbSet<LetteringGroup> LetteringGroups { get; set; } = null!;
        public DbSet<LetteringLine> LetteringLines { get; set; } = null!;
        public DbSet<CustomerPriceListItem> CustomerPriceListItems { get; set; } = null!;

        // Customers
        public async ValueTask<Customer> InsertCustomerAsync(Customer customer)
        {
            EntityEntry<Customer> entry = await this.Customers.AddAsync(customer);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<Customer> SelectAllCustomers() => this.Customers.AsQueryable();

        public async ValueTask<Customer?> SelectCustomerByIdAsync(int id) =>
            await this.Customers.FindAsync(id);

        public async ValueTask<Customer> UpdateCustomerAsync(Customer customer)
        {
            EntityEntry<Customer> entry = this.Customers.Update(customer);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask DeleteCustomerAsync(Customer customer)
        {
            this.Customers.Remove(customer);
            await this.SaveChangesAsync();
        }

        // Suppliers
        public async ValueTask<Supplier> InsertSupplierAsync(Supplier supplier)
        {
            EntityEntry<Supplier> entry = await this.Suppliers.AddAsync(supplier);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<Supplier> SelectAllSuppliers() => this.Suppliers.AsQueryable();

        public async ValueTask<Supplier?> SelectSupplierByIdAsync(int id) =>
            await this.Suppliers.FindAsync(id);

        public async ValueTask<Supplier> UpdateSupplierAsync(Supplier supplier)
        {
            EntityEntry<Supplier> entry = this.Suppliers.Update(supplier);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask DeleteSupplierAsync(Supplier supplier)
        {
            this.Suppliers.Remove(supplier);
            await this.SaveChangesAsync();
        }

        // Quotes
        public async ValueTask<Quote> InsertQuoteAsync(Quote quote)
        {
            EntityEntry<Quote> entry = await this.Quotes.AddAsync(quote);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<Quote> SelectAllQuotes() =>
            this.Quotes.Include(q => q.Customer).Include(q => q.Lines)
                .Where(q => !q.IsDeleted && !q.IsArchived).AsQueryable();

        public IQueryable<Quote> SelectDeletedQuotes() =>
            this.Quotes.Include(q => q.Customer).Include(q => q.Lines)
                .Where(q => q.IsDeleted).AsQueryable();

        public async ValueTask<Quote?> SelectQuoteByIdAsync(int id) =>
            await this.Quotes.Include(q => q.Customer).Include(q => q.Lines)
                .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted);

        public async ValueTask<Quote?> SelectQuoteByIdIncludingDeletedAsync(int id) =>
            await this.Quotes.Include(q => q.Customer).Include(q => q.Lines)
                .FirstOrDefaultAsync(q => q.Id == id);

        public async ValueTask<Quote> UpdateQuoteAsync(Quote quote)
        {
            EntityEntry<Quote> entry = this.Quotes.Update(quote);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask DeleteQuoteAsync(Quote quote)
        {
            quote.IsDeleted = true;
            quote.DeletedAt = System.DateTime.UtcNow;
            this.Quotes.Update(quote);
            await this.SaveChangesAsync();
        }

        public async ValueTask PurgeQuoteAsync(Quote quote)
        {
            this.Quotes.Remove(quote);
            await this.SaveChangesAsync();
        }

        // Sales Orders
        public async ValueTask<SalesOrder> InsertSalesOrderAsync(SalesOrder salesOrder)
        {
            EntityEntry<SalesOrder> entry = await this.SalesOrders.AddAsync(salesOrder);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<SalesOrder> SelectAllSalesOrders() =>
            this.SalesOrders.Include(o => o.Customer).Include(o => o.Lines)
                .Where(o => !o.IsDeleted && !o.IsArchived).AsQueryable();

        public IQueryable<SalesOrder> SelectDeletedSalesOrders() =>
            this.SalesOrders.Include(o => o.Customer).Include(o => o.Lines)
                .Where(o => o.IsDeleted).AsQueryable();

        public async ValueTask<SalesOrder?> SelectSalesOrderByIdAsync(int id) =>
            await this.SalesOrders.Include(o => o.Customer).Include(o => o.Lines)
                .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);

        public async ValueTask<SalesOrder?> SelectSalesOrderByIdIncludingDeletedAsync(int id) =>
            await this.SalesOrders.Include(o => o.Customer).Include(o => o.Lines)
                .FirstOrDefaultAsync(o => o.Id == id);

        public async ValueTask<SalesOrder> UpdateSalesOrderAsync(SalesOrder salesOrder)
        {
            EntityEntry<SalesOrder> entry = this.SalesOrders.Update(salesOrder);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask DeleteSalesOrderAsync(SalesOrder salesOrder)
        {
            // Soft-delete par défaut ; purge physique uniquement via corbeille (Draft).
            salesOrder.IsDeleted = true;
            salesOrder.DeletedAt = System.DateTime.UtcNow;
            this.SalesOrders.Update(salesOrder);
            await this.SaveChangesAsync();
        }

        public async ValueTask PurgeSalesOrderAsync(SalesOrder salesOrder)
        {
            this.SalesOrders.Remove(salesOrder);
            await this.SaveChangesAsync();
        }

        // Sales Invoices
        public async ValueTask<SalesInvoice> InsertSalesInvoiceAsync(SalesInvoice salesInvoice)
        {
            EntityEntry<SalesInvoice> entry = await this.SalesInvoices.AddAsync(salesInvoice);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<SalesInvoice> SelectAllSalesInvoices() =>
            this.SalesInvoices.Include(i => i.Customer).Include(i => i.Lines)
                .Where(i => !i.IsDeleted && !i.IsArchived).AsQueryable();

        public IQueryable<SalesInvoice> SelectDeletedSalesInvoices() =>
            this.SalesInvoices.Include(i => i.Customer).Include(i => i.Lines)
                .Where(i => i.IsDeleted).AsQueryable();

        public async ValueTask<SalesInvoice?> SelectSalesInvoiceByIdAsync(int id) =>
            await this.SalesInvoices.Include(i => i.Customer).Include(i => i.Lines)
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);

        public async ValueTask<SalesInvoice?> SelectSalesInvoiceByIdIncludingDeletedAsync(int id) =>
            await this.SalesInvoices.Include(i => i.Customer).Include(i => i.Lines)
                .FirstOrDefaultAsync(i => i.Id == id);

        public async ValueTask<SalesInvoice> UpdateSalesInvoiceAsync(SalesInvoice salesInvoice)
        {
            EntityEntry<SalesInvoice> entry = this.SalesInvoices.Update(salesInvoice);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask DeleteSalesInvoiceAsync(SalesInvoice salesInvoice)
        {
            salesInvoice.IsDeleted = true;
            salesInvoice.DeletedAt = System.DateTime.UtcNow;
            this.SalesInvoices.Update(salesInvoice);
            await this.SaveChangesAsync();
        }

        public async ValueTask PurgeSalesInvoiceAsync(SalesInvoice salesInvoice)
        {
            this.SalesInvoices.Remove(salesInvoice);
            await this.SaveChangesAsync();
        }

        // Credit Notes
        public async ValueTask<CreditNoteEntity> InsertCreditNoteAsync(CreditNoteEntity creditNote)
        {
            EntityEntry<CreditNoteEntity> entry = await this.CreditNotes.AddAsync(creditNote);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<CreditNoteEntity> SelectAllCreditNotes() =>
            this.CreditNotes.Include(c => c.Customer).Include(c => c.Lines)
                .Where(c => !c.IsDeleted && !c.IsArchived).AsQueryable();

        public async ValueTask<CreditNoteEntity?> SelectCreditNoteByIdAsync(int id) =>
            await this.CreditNotes.Include(c => c.Customer).Include(c => c.Lines)
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

        public async ValueTask<CreditNoteEntity> UpdateCreditNoteAsync(CreditNoteEntity creditNote)
        {
            EntityEntry<CreditNoteEntity> entry = this.CreditNotes.Update(creditNote);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        // Purchase Orders
        public async ValueTask<PurchaseOrder> InsertPurchaseOrderAsync(PurchaseOrder purchaseOrder)
        {
            EntityEntry<PurchaseOrder> entry = await this.PurchaseOrders.AddAsync(purchaseOrder);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<PurchaseOrder> SelectAllPurchaseOrders() => this.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Lines)
            .AsQueryable();

        public async ValueTask<PurchaseOrder?> SelectPurchaseOrderByIdAsync(int id) =>
            await this.PurchaseOrders
                .Include(p => p.Supplier)
                .Include(p => p.Lines)
                .FirstOrDefaultAsync(p => p.Id == id);

        public async ValueTask<PurchaseOrder> UpdatePurchaseOrderAsync(PurchaseOrder purchaseOrder)
        {
            EntityEntry<PurchaseOrder> entry = this.PurchaseOrders.Update(purchaseOrder);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        // Supplier Invoices
        public async ValueTask<SupplierInvoiceEntity> InsertSupplierInvoiceAsync(SupplierInvoiceEntity supplierInvoice)
        {
            EntityEntry<SupplierInvoiceEntity> entry = await this.SupplierInvoices.AddAsync(supplierInvoice);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<SupplierInvoiceEntity> SelectAllSupplierInvoices() => this.SupplierInvoices
            .Include(s => s.Supplier)
            .Include(s => s.PurchaseOrder)
            .Include(s => s.Receipt)
            .Include(s => s.Lines)
            .AsQueryable();

        public async ValueTask<SupplierInvoiceEntity?> SelectSupplierInvoiceByIdAsync(int id) =>
            await this.SupplierInvoices
                .Include(s => s.Supplier)
                .Include(s => s.PurchaseOrder)
                .Include(s => s.Receipt)
                .Include(s => s.Lines)
                .FirstOrDefaultAsync(s => s.Id == id);

        public async ValueTask<SupplierInvoiceEntity> UpdateSupplierInvoiceAsync(SupplierInvoiceEntity supplierInvoice)
        {
            EntityEntry<SupplierInvoiceEntity> entry = this.SupplierInvoices.Update(supplierInvoice);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask<SupplierPayment> InsertSupplierPaymentAsync(SupplierPayment payment)
        {
            EntityEntry<SupplierPayment> entry = await this.SupplierPayments.AddAsync(payment);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<SupplierPayment> SelectAllSupplierPayments() => this.SupplierPayments.AsQueryable();

        public async ValueTask<SupplierPayment?> SelectSupplierPaymentByIdAsync(int id) =>
            await this.SupplierPayments.FirstOrDefaultAsync(p => p.Id == id);

        public async ValueTask<SupplierPayment> UpdateSupplierPaymentAsync(SupplierPayment payment)
        {
            EntityEntry<SupplierPayment> entry = this.SupplierPayments.Update(payment);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        // Stock Movements
        public async ValueTask<StockMovement> InsertStockMovementAsync(StockMovement movement)
        {
            EntityEntry<StockMovement> entry = await this.StockMovements.AddAsync(movement);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<StockMovement> SelectAllStockMovements() => this.StockMovements.AsQueryable();

        public async ValueTask<Backup.Web.Api.Server.Models.StockItem> InsertStockAsync(Backup.Web.Api.Server.Models.StockItem stockItem)
        {
            var entry = await this.Stock.AddAsync(stockItem);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask<Backup.Web.Api.Server.Models.StockItem> UpdateStockAsync(Backup.Web.Api.Server.Models.StockItem stockItem)
        {
            var entry = this.Stock.Update(stockItem);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        // Document Number Sequences
        public async ValueTask<DocumentNumberSequence> InsertNumberSequenceAsync(DocumentNumberSequence sequence)
        {
            EntityEntry<DocumentNumberSequence> entry = await this.DocumentNumberSequences.AddAsync(sequence);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<DocumentNumberSequence> SelectAllNumberSequences() => this.DocumentNumberSequences.AsQueryable();

        public async ValueTask<DocumentNumberSequence?> SelectNumberSequenceByTypeAsync(string documentType, string? companyId) =>
            await this.DocumentNumberSequences.FirstOrDefaultAsync(s => s.DocumentType == documentType && s.CompanyId == companyId);

        public async ValueTask<DocumentNumberSequence> UpdateNumberSequenceAsync(DocumentNumberSequence sequence)
        {
            EntityEntry<DocumentNumberSequence> entry = this.DocumentNumberSequences.Update(sequence);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        // Cash Sessions & Operations
        public async ValueTask<CashSession> InsertCashSessionAsync(CashSession session)
        {
            EntityEntry<CashSession> entry = await this.CashSessions.AddAsync(session);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<CashSession> SelectAllCashSessions() => this.CashSessions.Include(c => c.Operations).AsQueryable();

        public async ValueTask<CashSession?> SelectCashSessionByIdAsync(int id) =>
            await this.CashSessions.Include(c => c.Operations).FirstOrDefaultAsync(c => c.Id == id);

        public async ValueTask<CashSession?> SelectActiveCashSessionAsync(string? companyId) =>
            await this.CashSessions.Include(c => c.Operations).FirstOrDefaultAsync(c => c.Status == "Open" && c.CompanyId == companyId);

        public async ValueTask<CashSession> UpdateCashSessionAsync(CashSession session)
        {
            EntityEntry<CashSession> entry = this.CashSessions.Update(session);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask<CashOperation> InsertCashOperationAsync(CashOperation operation)
        {
            EntityEntry<CashOperation> entry = await this.CashOperations.AddAsync(operation);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<CashOperation> SelectCashOperationsBySessionId(int sessionId) =>
            this.CashOperations.Where(o => o.CashSessionId == sessionId).AsQueryable();

        // Payments
        public async ValueTask<Payment> InsertPaymentAsync(Payment payment)
        {
            EntityEntry<Payment> entry = await this.Payments.AddAsync(payment);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<Payment> SelectAllPayments() => this.Payments.AsNoTracking().AsQueryable();

        public async ValueTask<Payment?> SelectPaymentByIdAsync(int id) =>
            await this.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);

        public async ValueTask<Payment?> SelectPaymentByIdForUpdateAsync(int id) =>
            await this.Payments.FirstOrDefaultAsync(p => p.Id == id);

        public async ValueTask<Payment> UpdatePaymentAsync(Payment payment)
        {
            EntityEntry<Payment> entry = this.Payments.Update(payment);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<Payment> SelectPaymentsBySalesInvoiceId(int salesInvoiceId) =>
            this.Payments.AsNoTracking().Where(p => p.SalesInvoiceId == salesInvoiceId).AsQueryable();

        // Receipts (ErpReceipts)
        public async ValueTask<Receipt> InsertReceiptAsync(Receipt receipt)
        {
            EntityEntry<Receipt> entry = await this.Receipts.AddAsync(receipt);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<Receipt> SelectAllReceipts() => this.Receipts
            .Include(r => r.Supplier)
            .Include(r => r.PurchaseOrder)
            .Include(r => r.Lines)
            .AsQueryable();

        public async ValueTask<Receipt?> SelectReceiptByIdAsync(int id) =>
            await this.Receipts
                .Include(r => r.Supplier)
                .Include(r => r.PurchaseOrder)
                .Include(r => r.Lines)
                .FirstOrDefaultAsync(r => r.Id == id);

        public async ValueTask<Receipt?> SelectReceiptByDocumentIdAsync(int documentId) =>
            await this.Receipts
                .Include(r => r.Supplier)
                .Include(r => r.Lines)
                .FirstOrDefaultAsync(r => r.DocumentId == documentId);

        public async ValueTask<Receipt> UpdateReceiptAsync(Receipt receipt)
        {
            EntityEntry<Receipt> entry = this.Receipts.Update(receipt);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        // Sales Delivery Notes
        public async ValueTask<SalesDeliveryNote> InsertSalesDeliveryNoteAsync(SalesDeliveryNote note)
        {
            var entry = await this.SalesDeliveryNotes.AddAsync(note);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<SalesDeliveryNote> SelectAllSalesDeliveryNotes() =>
            this.SalesDeliveryNotes
                .Include(n => n.Customer)
                .Include(n => n.SalesOrder)
                .Include(n => n.Lines)
                .Where(n => !n.IsDeleted && !n.IsArchived)
                .AsQueryable();

        public IQueryable<SalesDeliveryNote> SelectDeletedSalesDeliveryNotes() =>
            this.SalesDeliveryNotes
                .Include(n => n.Customer)
                .Include(n => n.SalesOrder)
                .Include(n => n.Lines)
                .Where(n => n.IsDeleted)
                .AsQueryable();

        public async ValueTask<SalesDeliveryNote?> SelectSalesDeliveryNoteByIdAsync(int id) =>
            await this.SalesDeliveryNotes
                .Include(n => n.Customer)
                .Include(n => n.SalesOrder)
                .Include(n => n.Lines)
                .FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted);

        public async ValueTask<SalesDeliveryNote?> SelectSalesDeliveryNoteByIdIncludingDeletedAsync(int id) =>
            await this.SalesDeliveryNotes
                .Include(n => n.Customer)
                .Include(n => n.SalesOrder)
                .Include(n => n.Lines)
                .FirstOrDefaultAsync(n => n.Id == id);

        public async ValueTask<SalesDeliveryNote> UpdateSalesDeliveryNoteAsync(SalesDeliveryNote note)
        {
            var entry = this.SalesDeliveryNotes.Update(note);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask DeleteSalesDeliveryNoteAsync(SalesDeliveryNote note)
        {
            note.IsDeleted = true;
            note.DeletedAt = System.DateTime.UtcNow;
            this.SalesDeliveryNotes.Update(note);
            await this.SaveChangesAsync();
        }

        public async ValueTask PurgeSalesDeliveryNoteAsync(SalesDeliveryNote note)
        {
            this.SalesDeliveryNotes.Remove(note);
            await this.SaveChangesAsync();
        }

        // Document audit (P3)
        public async ValueTask<DocumentAuditLog> InsertDocumentAuditLogAsync(DocumentAuditLog log)
        {
            EntityEntry<DocumentAuditLog> entry = await this.DocumentAuditLogs.AddAsync(log);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<DocumentAuditLog> SelectAllDocumentAuditLogs() =>
            this.DocumentAuditLogs.AsQueryable();

        public IQueryable<EntityAuditLog> SelectAllEntityAuditLogs() =>
            this.EntityAuditLogs.AsQueryable();

        // Accounting entries
        public async ValueTask<AccountingEntry> InsertAccountingEntryAsync(AccountingEntry entry)
        {
            EntityEntry<AccountingEntry> added = await this.AccountingEntries.AddAsync(entry);
            await this.SaveChangesAsync();
            return added.Entity;
        }

        public IQueryable<AccountingEntry> SelectAllAccountingEntries() =>
            this.AccountingEntries.Include(e => e.Lines).AsQueryable();

        public async ValueTask<AccountingEntry?> SelectAccountingEntryByIdAsync(int id) =>
            await this.AccountingEntries.Include(e => e.Lines).FirstOrDefaultAsync(e => e.Id == id);

        public async ValueTask<AccountingEntry> UpdateAccountingEntryAsync(AccountingEntry entry)
        {
            EntityEntry<AccountingEntry> updated = this.AccountingEntries.Update(entry);
            await this.SaveChangesAsync();
            return updated.Entity;
        }

        public async ValueTask DeleteAccountingEntryAsync(AccountingEntry entry)
        {
            this.AccountingEntries.Remove(entry);
            await this.SaveChangesAsync();
        }

        // Sales Returns (BRC vente)
        public async ValueTask<SalesReturn> InsertSalesReturnAsync(SalesReturn salesReturn)
        {
            var entry = await this.SalesReturns.AddAsync(salesReturn);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<SalesReturn> SelectAllSalesReturns() =>
            this.SalesReturns
                .Include(r => r.Customer)
                .Include(r => r.SalesDeliveryNote)
                .Include(r => r.SalesOrder)
                .Include(r => r.Lines)
                .Where(r => !r.IsDeleted && !r.IsArchived)
                .AsQueryable();

        public async ValueTask<SalesReturn?> SelectSalesReturnByIdAsync(int id) =>
            await this.SalesReturns
                .Include(r => r.Customer)
                .Include(r => r.SalesDeliveryNote)
                .Include(r => r.SalesOrder)
                .Include(r => r.Lines)
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

        public async ValueTask<SalesReturn> UpdateSalesReturnAsync(SalesReturn salesReturn)
        {
            var entry = this.SalesReturns.Update(salesReturn);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask DeleteSalesReturnAsync(SalesReturn salesReturn)
        {
            salesReturn.IsDeleted = true;
            salesReturn.DeletedAt = System.DateTime.UtcNow;
            this.SalesReturns.Update(salesReturn);
            await this.SaveChangesAsync();
        }

        // Supplier Credit Notes (AF achat)
        public async ValueTask<SupplierCreditNoteEntity> InsertSupplierCreditNoteAsync(SupplierCreditNoteEntity creditNote)
        {
            var entry = await this.SupplierCreditNotes.AddAsync(creditNote);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<SupplierCreditNoteEntity> SelectAllSupplierCreditNotes() => this.SupplierCreditNotes
            .Include(c => c.Supplier)
            .Include(c => c.SupplierInvoice)
            .Include(c => c.Lines)
            .AsQueryable();

        public async ValueTask<SupplierCreditNoteEntity?> SelectSupplierCreditNoteByIdAsync(int id) =>
            await this.SupplierCreditNotes
                .Include(c => c.Supplier)
                .Include(c => c.SupplierInvoice)
                .Include(c => c.Lines)
                .FirstOrDefaultAsync(c => c.Id == id);

        public async ValueTask<SupplierCreditNoteEntity> UpdateSupplierCreditNoteAsync(SupplierCreditNoteEntity creditNote)
        {
            var entry = this.SupplierCreditNotes.Update(creditNote);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        // Proformas (PF vente)
        public async ValueTask<Proforma> InsertProformaAsync(Proforma proforma)
        {
            var entry = await this.Proformas.AddAsync(proforma);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<Proforma> SelectAllProformas() =>
            this.Proformas
                .Include(p => p.Customer)
                .Include(p => p.Lines)
                .Where(p => !p.IsDeleted)
                .AsQueryable();

        public async ValueTask<Proforma?> SelectProformaByIdAsync(int id) =>
            await this.Proformas
                .Include(p => p.Customer)
                .Include(p => p.Lines)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        public async ValueTask<Proforma> UpdateProformaAsync(Proforma proforma)
        {
            var entry = this.Proformas.Update(proforma);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask DeleteProformaAsync(Proforma proforma)
        {
            proforma.IsDeleted = true;
            proforma.DeletedAt = System.DateTime.UtcNow;
            this.Proformas.Update(proforma);
            await this.SaveChangesAsync();
        }

        // Deposit Invoices / Acomptes (AAC vente)
        public async ValueTask<DepositInvoice> InsertDepositInvoiceAsync(DepositInvoice deposit)
        {
            var entry = await this.DepositInvoices.AddAsync(deposit);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<DepositInvoice> SelectAllDepositInvoices() =>
            this.DepositInvoices
                .Include(d => d.Customer)
                .Include(d => d.SalesOrder)
                .AsQueryable();

        public async ValueTask<DepositInvoice?> SelectDepositInvoiceByIdAsync(int id) =>
            await this.DepositInvoices
                .Include(d => d.Customer)
                .Include(d => d.SalesOrder)
                .FirstOrDefaultAsync(d => d.Id == id);

        public async ValueTask<DepositInvoice> UpdateDepositInvoiceAsync(DepositInvoice deposit)
        {
            var entry = this.DepositInvoices.Update(deposit);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        // Supplier RFQ (DPF achat)
        public async ValueTask<SupplierRfq> InsertSupplierRfqAsync(SupplierRfq rfq)
        {
            var entry = await this.SupplierRfqs.AddAsync(rfq);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<SupplierRfq> SelectAllSupplierRfqs() =>
            this.SupplierRfqs
                .Include(r => r.Supplier)
                .Include(r => r.Lines)
                .Where(r => !r.IsDeleted)
                .AsQueryable();

        public async ValueTask<SupplierRfq?> SelectSupplierRfqByIdAsync(int id) =>
            await this.SupplierRfqs
                .Include(r => r.Supplier)
                .Include(r => r.Lines)
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

        public async ValueTask<SupplierRfq> UpdateSupplierRfqAsync(SupplierRfq rfq)
        {
            var entry = this.SupplierRfqs.Update(rfq);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask DeleteSupplierRfqAsync(SupplierRfq rfq)
        {
            rfq.IsDeleted = true;
            rfq.DeletedAt = System.DateTime.UtcNow;
            this.SupplierRfqs.Update(rfq);
            await this.SaveChangesAsync();
        }

        // Supplier Returns (BRF achat)
        public async ValueTask<SupplierReturn> InsertSupplierReturnAsync(SupplierReturn supplierReturn)
        {
            var entry = await this.SupplierReturns.AddAsync(supplierReturn);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<SupplierReturn> SelectAllSupplierReturns() =>
            this.SupplierReturns
                .Include(r => r.Supplier)
                .Include(r => r.PurchaseOrder)
                .Include(r => r.Receipt)
                .Include(r => r.SupplierInvoice)
                .Include(r => r.Lines)
                .Where(r => !r.IsDeleted)
                .AsQueryable();

        public async ValueTask<SupplierReturn?> SelectSupplierReturnByIdAsync(int id) =>
            await this.SupplierReturns
                .Include(r => r.Supplier)
                .Include(r => r.PurchaseOrder)
                .Include(r => r.Receipt)
                .Include(r => r.SupplierInvoice)
                .Include(r => r.Lines)
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

        public async ValueTask<SupplierReturn> UpdateSupplierReturnAsync(SupplierReturn supplierReturn)
        {
            var entry = this.SupplierReturns.Update(supplierReturn);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask DeleteSupplierReturnAsync(SupplierReturn supplierReturn)
        {
            supplierReturn.IsDeleted = true;
            supplierReturn.DeletedAt = System.DateTime.UtcNow;
            this.SupplierReturns.Update(supplierReturn);
            await this.SaveChangesAsync();
        }

        // Payment Allocations (RG-RG2 lite — audit paiement par lot)
        public async ValueTask<PaymentAllocation> InsertPaymentAllocationAsync(PaymentAllocation allocation)
        {
            var entry = await this.PaymentAllocations.AddAsync(allocation);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<PaymentAllocation> SelectAllPaymentAllocations() => this.PaymentAllocations.AsQueryable();

        // Lettering (RG-LT1–4 lite)
        public async ValueTask<LetteringGroup> InsertLetteringGroupAsync(LetteringGroup group)
        {
            var entry = await this.LetteringGroups.AddAsync(group);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<LetteringGroup> SelectAllLetteringGroups() =>
            this.LetteringGroups.Include(g => g.Customer).Include(g => g.Lines).AsQueryable();

        public async ValueTask<LetteringGroup?> SelectLetteringGroupByIdAsync(int id) =>
            await this.LetteringGroups.Include(g => g.Customer).Include(g => g.Lines)
                .FirstOrDefaultAsync(g => g.Id == id);

        public async ValueTask<LetteringGroup> UpdateLetteringGroupAsync(LetteringGroup group)
        {
            var entry = this.LetteringGroups.Update(group);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        // Customer Price List (RG-PT1–5 lite)
        public async ValueTask<CustomerPriceListItem> InsertCustomerPriceListItemAsync(CustomerPriceListItem item)
        {
            var entry = await this.CustomerPriceListItems.AddAsync(item);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<CustomerPriceListItem> SelectAllCustomerPriceListItems() => this.CustomerPriceListItems.AsQueryable();

        public async ValueTask<CustomerPriceListItem?> SelectCustomerPriceListItemByIdAsync(int id) =>
            await this.CustomerPriceListItems.FirstOrDefaultAsync(p => p.Id == id);

        public async ValueTask<CustomerPriceListItem> UpdateCustomerPriceListItemAsync(CustomerPriceListItem item)
        {
            var entry = this.CustomerPriceListItems.Update(item);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask DeleteCustomerPriceListItemAsync(CustomerPriceListItem item)
        {
            this.CustomerPriceListItems.Remove(item);
            await this.SaveChangesAsync();
        }
    }
}
