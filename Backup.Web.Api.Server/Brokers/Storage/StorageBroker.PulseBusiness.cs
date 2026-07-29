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
        public DbSet<StockMovement> StockMovements { get; set; } = null!;
        public DbSet<DocumentNumberSequence> DocumentNumberSequences { get; set; } = null!;
        public DbSet<CashSession> CashSessions { get; set; } = null!;
        public DbSet<CashOperation> CashOperations { get; set; } = null!;
        public DbSet<Receipt> Receipts { get; set; } = null!;
        public DbSet<ReceiptLine> ReceiptLines { get; set; } = null!;
        public DbSet<SalesDeliveryNote> SalesDeliveryNotes { get; set; } = null!;
        public DbSet<SalesDeliveryNoteLine> SalesDeliveryNoteLines { get; set; } = null!;

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

        public IQueryable<Quote> SelectAllQuotes() => this.Quotes.Include(q => q.Customer).Include(q => q.Lines).AsQueryable();

        public async ValueTask<Quote?> SelectQuoteByIdAsync(int id) =>
            await this.Quotes.Include(q => q.Customer).Include(q => q.Lines).FirstOrDefaultAsync(q => q.Id == id);

        public async ValueTask<Quote> UpdateQuoteAsync(Quote quote)
        {
            EntityEntry<Quote> entry = this.Quotes.Update(quote);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        // Sales Orders
        public async ValueTask<SalesOrder> InsertSalesOrderAsync(SalesOrder salesOrder)
        {
            EntityEntry<SalesOrder> entry = await this.SalesOrders.AddAsync(salesOrder);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<SalesOrder> SelectAllSalesOrders() => this.SalesOrders.Include(o => o.Customer).Include(o => o.Lines).AsQueryable();

        public async ValueTask<SalesOrder?> SelectSalesOrderByIdAsync(int id) =>
            await this.SalesOrders.Include(o => o.Customer).Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id);

        public async ValueTask<SalesOrder> UpdateSalesOrderAsync(SalesOrder salesOrder)
        {
            EntityEntry<SalesOrder> entry = this.SalesOrders.Update(salesOrder);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        // Sales Invoices
        public async ValueTask<SalesInvoice> InsertSalesInvoiceAsync(SalesInvoice salesInvoice)
        {
            EntityEntry<SalesInvoice> entry = await this.SalesInvoices.AddAsync(salesInvoice);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<SalesInvoice> SelectAllSalesInvoices() => this.SalesInvoices.Include(i => i.Customer).Include(i => i.Lines).AsQueryable();

        public async ValueTask<SalesInvoice?> SelectSalesInvoiceByIdAsync(int id) =>
            await this.SalesInvoices.Include(i => i.Customer).Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == id);

        public async ValueTask<SalesInvoice> UpdateSalesInvoiceAsync(SalesInvoice salesInvoice)
        {
            EntityEntry<SalesInvoice> entry = this.SalesInvoices.Update(salesInvoice);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        // Credit Notes
        public async ValueTask<CreditNoteEntity> InsertCreditNoteAsync(CreditNoteEntity creditNote)
        {
            EntityEntry<CreditNoteEntity> entry = await this.CreditNotes.AddAsync(creditNote);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<CreditNoteEntity> SelectAllCreditNotes() => this.CreditNotes.Include(c => c.Customer).Include(c => c.Lines).AsQueryable();

        public async ValueTask<CreditNoteEntity?> SelectCreditNoteByIdAsync(int id) =>
            await this.CreditNotes.Include(c => c.Customer).Include(c => c.Lines).FirstOrDefaultAsync(c => c.Id == id);

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
            .Include(s => s.Lines)
            .AsQueryable();

        public async ValueTask<SupplierInvoiceEntity?> SelectSupplierInvoiceByIdAsync(int id) =>
            await this.SupplierInvoices
                .Include(s => s.Supplier)
                .Include(s => s.PurchaseOrder)
                .Include(s => s.Lines)
                .FirstOrDefaultAsync(s => s.Id == id);

        public async ValueTask<SupplierInvoiceEntity> UpdateSupplierInvoiceAsync(SupplierInvoiceEntity supplierInvoice)
        {
            EntityEntry<SupplierInvoiceEntity> entry = this.SupplierInvoices.Update(supplierInvoice);
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
                .AsQueryable();

        public async ValueTask<SalesDeliveryNote?> SelectSalesDeliveryNoteByIdAsync(int id) =>
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
            this.SalesDeliveryNotes.Remove(note);
            await this.SaveChangesAsync();
        }
    }
}
